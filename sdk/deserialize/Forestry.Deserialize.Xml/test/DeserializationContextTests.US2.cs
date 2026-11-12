using Forestry.Deserialize.Reading;
using Forestry.Deserialize.Xml.Tests.Fixtures;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    /// <summary>
    /// Spec 002, User Story 2: containment, parent-child and sibling relationships (tasks.md T033-T040;
    /// quickstart 3, 8, 9, 15, 16)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4), so they do not compile until T042-T044 declare the
    /// surface they assume (research R21, R29, contracts/public-api.md). For the review gate (T041), the
    /// assumed surface, beyond US1's:
    /// <list type="bullet">
    /// <item><c>ContextRow.Container</c>: a <c>ContainerRelationship</c> with <c>long Id</c> and
    /// <c>long? ParentId</c>, the innermost open row's id when the row was constructed (null for none).</item>
    /// <item><c>DeserializationContext.Parent(ContextRow)</c>, <c>Children(ContextRow)</c> and
    /// <c>Siblings(ContextRow)</c>: by parent id, among the rows still in the context (open or kept);
    /// <c>Parent</c> returns null when the parent left.</item>
    /// <item>XML members stay direct children of their row's element (R29 option A): a value inside a
    /// wrapper is reached by adding the wrapper as a row type that is not enumerated, whose policy sets what
    /// its parent needs.</item>
    /// </list>
    /// Policies are <see cref="ContextPolicy"/> instances whose hooks each test writes in place. Theories run
    /// both entry points.
    /// </remarks>
    public sealed class DeserializationContextTestsUS2
    {
        private static DeserializeXmlOptions OptionsWith(params Type[] rowTypes)
        {
            DeserializeXmlOptions options = new();
            options.Rows.Add(rowTypes);
            return options;
        }

        /// <summary>
        /// A read-only stream over bytes, counting what is read from it. It wraps a
        /// <see cref="MemoryStream"/> rather than deriving from one, whose async reads call its own sync
        /// reads and would count each byte twice
        /// </summary>
        private sealed class CountingStream(byte[] bytes) : Stream
        {
            private readonly MemoryStream _inner = new(bytes, writable: false);

            public long BytesRead { get; private set; }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => _inner.Length;

            public override long Position
            {
                get => _inner.Position;
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count) => Count(_inner.Read(buffer, offset, count));

            public override int Read(Span<byte> buffer) => Count(_inner.Read(buffer));

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
                Count(await _inner.ReadAsync(buffer, cancellationToken));

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
                Count(await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _inner.Dispose();
                }

                base.Dispose(disposing);
            }

            private int Count(int read)
            {
                BytesRead += read;
                return read;
            }
        }

        // T033 - US2-1 (quickstart 3): an order's two lines come out before it, each with its own members
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnOrderWithTwoLines_ItShould_GiveTheLinesBeforeTheOrder(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                    <Line>
                      <LineNumber>1</LineNumber>
                      <Note>first</Note>
                    </Line>
                    <Line>
                      <LineNumber>2</LineNumber>
                      <Note>second</Note>
                    </Line>
                  </Order>
                </Root>
                """;

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith(typeof(Order), typeof(Line)));

            // Assert
            Assert.Equal(["Line", "Line", "Order"], rows.Select(row => row.GetType().Name));
            Assert.Equal([1, 2], rows.Take(2).Select(row => row.AsType<Line>().LineNumber));
            Assert.Equal(["first", "second"], rows.Take(2).Select(row => row.AsType<Line>().Note));
            Assert.Equal(1, rows[2].AsType<Order>().OrderNumber);
        }

        // T034 - US2-2 / FR-013: an order's member after its lines is set on the order, not on a line
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnOrderNumberAfterTheLines_ItShould_SetItOnTheOrder(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <Line>
                      <LineNumber>1</LineNumber>
                    </Line>
                    <OrderNumber>5</OrderNumber>
                  </Order>
                </Root>
                """;

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, OptionsWith(typeof(Order), typeof(Line)));

            // Assert
            Assert.Equal(0, rows[0].AsType<Line>().OrderNumber);
            Assert.Equal(5, rows[1].AsType<Order>().OrderNumber);
        }

        // T034 - FR-002 / edge case (quickstart 8): an empty line is a row like any other, its members unset
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnEmptyLine_ItShould_GiveARowWithNothingSet(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                    <Line />
                  </Order>
                </Root>
                """;
            List<bool> anyMemberSet = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order), typeof(Line));
            options.Policies.Add<Line>(new ContextPolicy
            {
                OnComplete = (row, context) => anyMemberSet.Add(
                    row.Definition.Properties.Any(property => row.IsSet(property)))
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(["Line", "Order"], rows.Select(row => row.GetType().Name));
            Assert.Equal([false], anyMemberSet);
        }

        // T035 - US2-3: a measurement that is not enumerated, inside an enumerated line, sees the line open
        // as its parent when it completes
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAMeasurementInsideALine_ItShould_SeeTheLineOpenAsItsParent(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Line>
                    <LineNumber>1</LineNumber>
                    <Measurement>
                      <Unit>kg</Unit>
                      <Amount>3</Amount>
                    </Measurement>
                  </Line>
                </Root>
                """;
            List<(bool IsLine, bool IsOpen)> parentSeen = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Line), typeof(Measurement));
            options.Policies.Add<Measurement>(new ContextPolicy
            {
                OnComplete = (row, context) =>
                {
                    ContextRow parent = context.Parent(row)!;
                    parentSeen.Add((parent.Row.Is<Line>(), parent.IsOpen));
                    row.Enumerate(false);
                }
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([(true, true)], parentSeen);
            Assert.True(Assert.Single(rows).Is<Line>());
        }

        // T036 - US2-4 / C2: several row types come out of one forward pass, each byte read once
        [Fact]
        public async Task DeserializeAsync_ForSeveralRowTypes_ItShould_ReadTheDocumentOnce()
        {
            // Arrange
            byte[] document = """
                <Root>
                  <Site>
                    <SiteKey>7</SiteKey>
                    <Order>
                      <OrderNumber>1</OrderNumber>
                      <Line>
                        <LineNumber>1</LineNumber>
                      </Line>
                    </Order>
                  </Site>
                </Root>
                """u8.ToArray();
            using CountingStream stream = new(document);

            // Act
            List<Row> rows = [];
            await foreach (Row row in Deserialization.DeserializeAsync(stream, OptionsWith(typeof(Site), typeof(Order), typeof(Line))))
            {
                rows.Add(row);
            }

            // Assert
            Assert.Equal(["Line", "Order", "Site"], rows.Select(row => row.GetType().Name));
            Assert.Equal(document.Length, stream.BytesRead);
        }

        // T037 - US2-5 (quickstart 9): the third kept line finds the first two as its siblings, closed, with the
        // order as their parent
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForThreeKeptLines_ItShould_GiveTheThirdItsTwoSiblings(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                    <Line>
                      <LineNumber>1</LineNumber>
                    </Line>
                    <Line>
                      <LineNumber>2</LineNumber>
                    </Line>
                    <Line>
                      <LineNumber>3</LineNumber>
                    </Line>
                  </Order>
                </Root>
                """;
            List<int> siblingsOfThird = [];
            List<bool> siblingsClosed = [];
            List<bool> siblingsParentIsOrder = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order), typeof(Line));
            options.Policies.Add<Line>(new ContextPolicy
            {
                OnComplete = (row, context) =>
                {
                    if (row.Row.AsType<Line>().LineNumber == 3)
                    {
                        foreach (ContextRow sibling in context.Siblings(row))
                        {
                            siblingsOfThird.Add(sibling.Row.AsType<Line>().LineNumber);
                            siblingsClosed.Add(!sibling.IsOpen);
                            siblingsParentIsOrder.Add(context.Parent(sibling)!.Row.Is<Order>());
                        }
                    }

                    row.Keep(Lifetime.UntilEnd);
                }
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([1, 2], siblingsOfThird);
            Assert.Equal([true, true], siblingsClosed);
            Assert.Equal([true, true], siblingsParentIsOrder);
        }

        // T037 - US2-6 (quickstart 9): at its completion, an order finds all its kept lines as children
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnOrderWithKeptLines_ItShould_FindThemAsChildren(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                    <Line>
                      <LineNumber>1</LineNumber>
                    </Line>
                    <Line>
                      <LineNumber>2</LineNumber>
                    </Line>
                  </Order>
                </Root>
                """;
            List<int> childrenOfOrder = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order), typeof(Line));
            options.Policies.Add<Line>(new ContextPolicy
            {
                OnComplete = (row, context) => row.Keep(Lifetime.UntilEnd)
            });
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (row, context) => childrenOfOrder.AddRange(
                    context.Children(row).Select(child => child.Row.AsType<Line>().LineNumber))
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([1, 2], childrenOfOrder);
        }

        // T037 - US2-6: at its completion, an order whose lines were not kept finds no children
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnOrderWithLinesNotKept_ItShould_FindNoChildren(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                    <Line>
                      <LineNumber>1</LineNumber>
                    </Line>
                    <Line>
                      <LineNumber>2</LineNumber>
                    </Line>
                  </Order>
                </Root>
                """;
            List<int> childrenOfOrder = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order), typeof(Line));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (row, context) => childrenOfOrder.Add(context.Children(row).Count())
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([0], childrenOfOrder);
        }

        // T038 - FR-014: the outermost row has no parent; an inner row's parent id is the row open around it
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnOrderAndItsLine_ItShould_RecordTheParentIds(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                    <Line>
                      <LineNumber>1</LineNumber>
                    </Line>
                  </Order>
                </Root>
                """;
            List<(long Id, long? ParentId)> lines = [];
            List<(long Id, long? ParentId)> orders = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order), typeof(Line));
            options.Policies.Add<Line>(new ContextPolicy
            {
                OnComplete = (row, context) => lines.Add((row.Container.Id, row.Container.ParentId))
            });
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (row, context) => orders.Add((row.Container.Id, row.Container.ParentId))
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Null(Assert.Single(orders).ParentId);
            Assert.Equal(orders[0].Id, Assert.Single(lines).ParentId);
        }

        // T038 - FR-015: a kept line whose order was not kept no longer finds its parent
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAKeptLineWhoseOrderLeft_ItShould_FindNoParent(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                    <Line>
                      <LineNumber>1</LineNumber>
                    </Line>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                </Root>
                """;
            List<bool> parentFound = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order), typeof(Line));
            options.Policies.Add<Line>(new ContextPolicy
            {
                OnComplete = (row, context) => row.Keep(Lifetime.UntilEnd)
            });
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (row, context) =>
                {
                    if (row.Row.AsType<Order>().OrderNumber == 2)
                    {
                        parentFound.Add(context.Parent(context.Rows<Line>().Single()) is not null);
                    }
                }
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([false], parentFound);
        }

        // T039 - edge case: a part nested in a part gives two rows, inner first, the outer one the inner one's parent
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAPartInsideAPart_ItShould_GiveTwoRowsInnerFirst(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Part>
                    <PartNumber>1</PartNumber>
                    <Part>
                      <PartNumber>2</PartNumber>
                    </Part>
                  </Part>
                </Root>
                """;
            List<int> parentOfInner = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Part));
            options.Policies.Add<Part>(new ContextPolicy
            {
                OnComplete = (row, context) =>
                {
                    if (row.Row.AsType<Part>().PartNumber == 2)
                    {
                        parentOfInner.Add(context.Parent(row)!.Row.AsType<Part>().PartNumber);
                    }
                }
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([2, 1], rows.Select(row => row.AsType<Part>().PartNumber));
            Assert.Equal([1], parentOfInner);
        }

        // T040 - R29 option A / FR-013 (quickstart 15): a member inside a wrapper is not a direct child of the
        // order, so it does not set the order's member
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnOrderNumberInsideAWrapper_ItShould_NotSetItOnTheOrder(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <Details>
                      <OrderNumber>5</OrderNumber>
                    </Details>
                  </Order>
                </Root>
                """;
            List<bool> orderNumberSet = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (row, context) => orderNumberSet.Add(row.IsSet(nameof(Order.OrderNumber)))
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([false], orderNumberSet);
            Assert.Equal(0, Assert.Single(rows).AsType<Order>().OrderNumber);
        }

        // T040 - R29 option A (quickstart 15): the wrapper added as a row type that is not enumerated, its
        // policy copying the member onto its parent, reaches the value
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForADetailsRowCopyingOntoItsOrder_ItShould_SetTheOrderNumber(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <Details>
                      <OrderNumber>5</OrderNumber>
                    </Details>
                  </Order>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith(typeof(Order), typeof(Details));
            options.Policies.Add<Details>(new ContextPolicy
            {
                OnComplete = (row, context) =>
                {
                    context.Parent(row)!.Set(nameof(Order.OrderNumber), row.Row.AsType<Details>().OrderNumber);
                    row.Enumerate(false);
                }
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(5, Assert.Single(rows).AsType<Order>().OrderNumber);
        }

        // T040 - R29 (quickstart 16): a wrapper with meaning, added as a row type that is not enumerated,
        // sets the right member on its parent
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAMeasurementInKilograms_ItShould_SetTheLinesWeightAmount(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Line>
                    <LineNumber>1</LineNumber>
                    <Measurement>
                      <Unit>kg</Unit>
                      <Amount>3</Amount>
                    </Measurement>
                  </Line>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith(typeof(Line), typeof(Measurement));
            options.Policies.Add<Measurement>(new ContextPolicy
            {
                OnComplete = (row, context) =>
                {
                    Measurement measurement = row.Row.AsType<Measurement>();

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
            Assert.Equal(3m, Assert.Single(rows).AsType<Line>().WeightAmount);
        }
    }
}
