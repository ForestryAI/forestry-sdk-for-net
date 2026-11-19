using Forestry.Deserialize.Xml.Tests.Fixtures;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    /// <summary>
    /// Spec 002, User Story 3: a node's own values as members (tasks.md T048-T053; quickstart 10, 11)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4), so they do not compile until T055 declares the surface
    /// they assume (research R30, contracts/public-api.md). Where a member's value comes from is the
    /// media's, so the attributes are the XML package's, not the core's (developer, 2026-11-12). For the
    /// review gate (T054), the assumed surface, in namespace <c>Forestry.Deserialize.Xml</c> (names
    /// Deferred; named in spec 003, research R37: one family after the part of the document):
    /// <list type="bullet">
    /// <item><c>[XmlAttributeName("name")]</c> (spec 002 <c>[XmlAttributeValue]</c>): a member set only from the row element's attribute of that
    /// name; a child element never sets it; attributes no member matches are ignored.</item>
    /// <item><c>[XmlElementText]</c> (spec 002 <c>[XmlTextValue]</c>): the member set only from the text directly inside the row element, the
    /// last text winning; with no text by the row's end it is set empty (null where the member allows it,
    /// else its default) and marked set. More than one per type fails when the options lock.</item>
    /// <item><c>DeserializeXmlOptions.ElementText&lt;TRow&gt;(memberName)</c> (spec 002 <c>TextValue</c>): the same through the options,
    /// winning over the attribute.</item>
    /// <item>A member with no marker (or <c>[XmlElementName]</c>, spec 002's core <c>[Field]</c>) is set only from a direct child element.</item>
    /// <item>Spec 003 (T021, FR-006a): a member has at most one source; two of <c>[XmlElementName]</c>,
    /// <c>[XmlAttributeName]</c>, <c>[XmlElementText]</c>, or <c>ElementText&lt;TRow&gt;</c> with either of the
    /// first two, fail when the options lock, naming the member and both sources.</item>
    /// </list>
    /// Theories run both entry points.
    /// </remarks>
    public sealed class DeserializationContextTestsUS3
    {
        private static DeserializeXmlOptions OptionsWith(params Type[] rowTypes)
        {
            DeserializeXmlOptions options = new();
            options.Rows.Add(rowTypes);
            return options;
        }

        // T048 - US3-1 / US3-2 / SC-004 (quickstart 10): three prices told apart by attributes, each with both
        // attributes converted and its own text as the amount
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForALineWithThreePrices_ItShould_GiveEachItsAttributesAndAmount(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Line>
                    <LineNumber>1</LineNumber>
                    <Price currency="EUR" kind="net">0.59</Price>
                    <Price currency="EUR" kind="gross">0.74</Price>
                    <Price currency="SEK" kind="net">6.50</Price>
                  </Line>
                </Root>
                """;

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith(typeof(Line), typeof(Price)));

            // Assert
            Price[] prices = [.. rows.Where(row => row.Is<Price>()).Select(row => row.AsType<Price>())];
            Assert.Equal(["EUR", "EUR", "SEK"], prices.Select(price => price.Currency));
            Assert.Equal(["net", "gross", "net"], prices.Select(price => price.Kind));
            Assert.Equal([0.59m, 0.74m, 6.50m], prices.Select(price => price.Amount));
            Assert.True(rows[^1].Is<Line>());
        }

        // T049 - US3-3: an attribute no member matches, and a prefixed one, are ignored
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForUnmatchedAttributes_ItShould_IgnoreThem(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
                  <Price currency="EUR" discount="5" xsi:type="Price" kind="net">0.59</Price>
                </Root>
                """;

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith(typeof(Price)));

            // Assert
            Price price = Assert.Single(rows).AsType<Price>();
            Assert.Equal("EUR", price.Currency);
            Assert.Equal("net", price.Kind);
            Assert.Equal(0.59m, price.Amount);
        }

        // T050 - US3-4 / FR-017 (quickstart 11): an empty element sets its attributes, and its own value
        // empty and set
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnEmptyReferenceWithAnAttribute_ItShould_SetTheAttributeAndAnEmptyValue(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Reference type="external" />
                </Root>
                """;
            List<bool> valueSet = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Reference));
            options.Policies.Add<Reference>(new ContextPolicy
            {
                OnComplete = (row, context) => valueSet.Add(row.IsSet(nameof(Reference.Value)))
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Reference reference = Assert.Single(rows).AsType<Reference>();
            Assert.Equal("external", reference.Type);
            Assert.Null(reference.Value);
            Assert.Equal([true], valueSet);
        }

        // T050 - FR-017: with text around a child element, the last text is the own value (for now: joining
        // the pieces, as XmlSerializer and XElement.Value do, is deferred in research R30)
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForTextAroundAChildElement_ItShould_KeepTheLastText(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Reference type="internal">first<Extra />second</Reference>
                </Root>
                """;

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith(typeof(Reference)));

            // Assert
            Assert.Equal("second", Assert.Single(rows).AsType<Reference>().Value);
        }

        // T051 - FR-018: two members read from the same name fail when the options lock, naming both
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForTwoMembersNamedCurrency_ItShould_ThrowBeforeAnyByteIsRead(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Price currency="EUR" />
                </Root>
                """;

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => EntryPoints.Read(entryPoint, document, OptionsWith(typeof(PriceWithDuplicateName))));

            // Assert
            Assert.Contains(nameof(PriceWithDuplicateName.Currency), exception.Message);
            Assert.Contains(nameof(PriceWithDuplicateName.Code), exception.Message);
        }

        // T051 - FR-018: a member from an attribute takes nothing from a child element of the same name
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForCurrencyAsAttributeAndChild_ItShould_KeepTheAttributesValue(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Price currency="EUR" kind="net">
                    <currency>SEK</currency>
                  </Price>
                </Root>
                """;

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith(typeof(Price)));

            // Assert
            Assert.Equal("EUR", Assert.Single(rows).AsType<Price>().Currency);
        }

        // T053 - FR-018 / R30: two members from the element's text fail when the options lock, naming the class
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForTwoTextMembers_ItShould_ThrowBeforeAnyByteIsRead(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Price>0.59</Price>
                </Root>
                """;

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => EntryPoints.Read(entryPoint, document, OptionsWith(typeof(PriceWithTwoTextValues))));

            // Assert
            Assert.Contains(nameof(PriceWithTwoTextValues), exception.Message);
        }

        // T053 - R30: the text member set through the options wins over the attribute
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForATextMemberSetThroughTheOptions_ItShould_WinOverTheAttribute(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Price>0.59</Price>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith(typeof(PriceWithTextOption));
            options.ElementText<PriceWithTextOption>(nameof(PriceWithTextOption.Other));

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            PriceWithTextOption price = Assert.Single(rows).AsType<PriceWithTextOption>();
            Assert.Equal(0.59m, price.Other);
            Assert.Equal(0m, price.Amount);
        }

        // Spec 003 T021 - US2-2b / FR-006a (quickstart 8): a member from a child element and from the row's
        // own text fails when the options lock, naming the member and both sources
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAMemberFromAnElementAndTheText_ItShould_ThrowNamingBothSources(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Price>0.59</Price>
                </Root>
                """;

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => EntryPoints.Read(entryPoint, document, OptionsWith(typeof(PriceWithElementAndText))));

            // Assert
            Assert.Contains(nameof(PriceWithElementAndText), exception.Message);
            Assert.Contains(nameof(PriceWithElementAndText.Amount), exception.Message);
            Assert.Contains("XmlElementName", exception.Message);
            Assert.Contains("XmlElementText", exception.Message);
        }

        // Spec 003 T021 - US2-2b / FR-006a: a member from an attribute and from a child element fails when
        // the options lock, naming the member and both sources
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAMemberFromAnAttributeAndAnElement_ItShould_ThrowNamingBothSources(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Price currency="EUR">
                    <Amount>0.59</Amount>
                  </Price>
                </Root>
                """;

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => EntryPoints.Read(entryPoint, document, OptionsWith(typeof(PriceWithAttributeAndElement))));

            // Assert
            Assert.Contains(nameof(PriceWithAttributeAndElement), exception.Message);
            Assert.Contains(nameof(PriceWithAttributeAndElement.Currency), exception.Message);
            Assert.Contains("XmlAttributeName", exception.Message);
            Assert.Contains("XmlElementName", exception.Message);
        }

        // Spec 003 T021 - US2-2b / FR-006a: a member from an attribute that the options also make the row's
        // own text fails when the options lock, naming the member, the attribute and the option
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnAttributeMemberMadeTheTextByTheOptions_ItShould_ThrowNamingBothSources(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Price currency="EUR">
                    <Amount>0.59</Amount>
                  </Price>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith(typeof(PriceWithAttributeAndTextOption));
            options.ElementText<PriceWithAttributeAndTextOption>(nameof(PriceWithAttributeAndTextOption.Currency));

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains(nameof(PriceWithAttributeAndTextOption), exception.Message);
            Assert.Contains(nameof(PriceWithAttributeAndTextOption.Currency), exception.Message);
            Assert.Contains("XmlAttributeName", exception.Message);
            Assert.Contains("ElementText", exception.Message);
        }

        // T052 - R29 with attributes: a wrapper row type's attribute is set before its children arrive, and
        // its policy sets the right member on its parent
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAMeasurementWithAUnitAttribute_ItShould_SetTheLinesWeightAmount(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Line>
                    <LineNumber>1</LineNumber>
                    <Measurement unit="kg">
                      <Amount>3</Amount>
                    </Measurement>
                  </Line>
                </Root>
                """;
            List<bool> unitSetBeforeAmount = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Line), typeof(MeasurementByAttribute));
            options.Policies.Add<MeasurementByAttribute>(new ContextPolicy
            {
                OnSet = (row, member, value, context) =>
                {
                    if (member.Name == nameof(MeasurementByAttribute.Amount))
                    {
                        unitSetBeforeAmount.Add(row.IsSet(nameof(MeasurementByAttribute.Unit)));
                    }
                },
                OnComplete = (row, context) =>
                {
                    MeasurementByAttribute measurement = row.Row.AsType<MeasurementByAttribute>();

                    if (measurement.Unit == "kg")
                    {
                        context.Parent(row)!.Set(nameof(Line.WeightAmount), measurement.Amount);
                    }

                    row.Enumerate(false);
                }
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([true], unitSetBeforeAmount);
            Assert.Equal(3m, Assert.Single(rows).AsType<Line>().WeightAmount);
        }
    }
}
