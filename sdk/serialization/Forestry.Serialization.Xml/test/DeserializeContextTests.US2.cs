using Forestry.Serialization.Reading;
using Forestry.Serialization.Xml.Tests.Fixtures;
using Xunit;

namespace Forestry.Serialization.Xml.Tests
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
    /// <item><c>ContextTelemetry.Container</c>: a <c>ContainerRelationship</c> with <c>long Id</c> and
    /// <c>long? ParentId</c>, the innermost open telemetry's id when the telemetry was constructed (null for none).</item>
    /// <item><c>DeserializeContext.Parent(ContextTelemetry)</c>, <c>Children(ContextTelemetry)</c> and
    /// <c>Siblings(ContextTelemetry)</c>: by parent id, among the telemetries still in the context (open or kept);
    /// <c>Parent</c> returns null when the parent left.</item>
    /// <item>XML members stay direct children of their telemetry's element (R29 option A): a value inside a
    /// wrapper is reached by adding the wrapper as a telemetry type that is not enumerated, whose policy sets what
    /// its parent needs.</item>
    /// </list>
    /// Policies are <see cref="ContextPolicy"/> instances whose hooks each test writes in place. Theories run
    /// both entry points.
    /// </remarks>
    public sealed class DeserializeContextTestsUS2
    {
        private static DeserializeXmlOptions OptionsWith(params Type[] telemetryTypes)
        {
            DeserializeXmlOptions options = new();
            options.Telemetries.Add(telemetryTypes);
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
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, OptionsWith(typeof(Order), typeof(Line)));

            // Assert
            Assert.Equal(["Line", "Line", "Order"], telemetries.Select(telemetry => telemetry.GetType().Name));
            Assert.Equal([1, 2], telemetries.Take(2).Select(telemetry => telemetry.AsType<Line>().LineNumber));
            Assert.Equal(["first", "second"], telemetries.Take(2).Select(telemetry => telemetry.AsType<Line>().Note));
            Assert.Equal(1, telemetries[2].AsType<Order>().OrderNumber);
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
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, OptionsWith(typeof(Order), typeof(Line)));

            // Assert
            Assert.Equal(0, telemetries[0].AsType<Line>().OrderNumber);
            Assert.Equal(5, telemetries[1].AsType<Order>().OrderNumber);
        }

        // T034 - FR-002 / edge case (quickstart 8): an empty line is a telemetry like any other, its members unset
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnEmptyLine_ItShould_GiveATelemetryWithNothingSet(EntryPoint entryPoint)
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
                OnComplete = (telemetry, context) => anyMemberSet.Add(
                    telemetry.Definition.Properties.Any(property => telemetry.IsSet(property)))
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(["Line", "Order"], telemetries.Select(telemetry => telemetry.GetType().Name));
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
                OnComplete = (telemetry, context) =>
                {
                    ContextTelemetry parent = context.Parent(telemetry)!;
                    parentSeen.Add((parent.Telemetry.Is<Line>(), parent.IsOpen));
                    telemetry.Enumerate(false);
                }
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([(true, true)], parentSeen);
            Assert.True(Assert.Single(telemetries).Is<Line>());
        }

        // T036 - US2-4 / C2: several telemetry types come out of one forward pass, each byte read once
        [Fact]
        public async Task DeserializeAsync_ForSeveralTelemetryTypes_ItShould_ReadTheDocumentOnce()
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
            List<Telemetry> telemetries = [];
            await foreach (Telemetry telemetry in TelemetrySerialization.DeserializeAsync(stream, OptionsWith(typeof(Site), typeof(Order), typeof(Line))))
            {
                telemetries.Add(telemetry);
            }

            // Assert
            Assert.Equal(["Line", "Order", "Site"], telemetries.Select(telemetry => telemetry.GetType().Name));
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
                OnComplete = (telemetry, context) =>
                {
                    if (telemetry.Telemetry.AsType<Line>().LineNumber == 3)
                    {
                        foreach (ContextTelemetry sibling in context.Siblings(telemetry))
                        {
                            siblingsOfThird.Add(sibling.Telemetry.AsType<Line>().LineNumber);
                            siblingsClosed.Add(!sibling.IsOpen);
                            siblingsParentIsOrder.Add(context.Parent(sibling)!.Telemetry.Is<Order>());
                        }
                    }

                    telemetry.Keep(Lifetime.UntilEnd);
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
                OnComplete = (telemetry, context) => telemetry.Keep(Lifetime.UntilEnd)
            });
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => childrenOfOrder.AddRange(
                    context.Children(telemetry).Select(child => child.Telemetry.AsType<Line>().LineNumber))
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
                OnComplete = (telemetry, context) => childrenOfOrder.Add(context.Children(telemetry).Count())
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([0], childrenOfOrder);
        }

        // T038 - FR-014: the outermost telemetry has no parent; an inner telemetry's parent id is the telemetry open around it
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
                OnComplete = (telemetry, context) => lines.Add((telemetry.Container.Id, telemetry.Container.ParentId))
            });
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => orders.Add((telemetry.Container.Id, telemetry.Container.ParentId))
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
                OnComplete = (telemetry, context) => telemetry.Keep(Lifetime.UntilEnd)
            });
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    if (telemetry.Telemetry.AsType<Order>().OrderNumber == 2)
                    {
                        parentFound.Add(context.Parent(context.Telemetries<Line>().Single()) is not null);
                    }
                }
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([false], parentFound);
        }

        // T039 - edge case: a part nested in a part gives two telemetries, inner first, the outer one the inner one's parent
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAPartInsideAPart_ItShould_GiveTwoTelemetriesInnerFirst(EntryPoint entryPoint)
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
                OnComplete = (telemetry, context) =>
                {
                    if (telemetry.Telemetry.AsType<Part>().PartNumber == 2)
                    {
                        parentOfInner.Add(context.Parent(telemetry)!.Telemetry.AsType<Part>().PartNumber);
                    }
                }
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([2, 1], telemetries.Select(telemetry => telemetry.AsType<Part>().PartNumber));
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
                OnComplete = (telemetry, context) => orderNumberSet.Add(telemetry.IsSet(nameof(Order.OrderNumber)))
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([false], orderNumberSet);
            Assert.Equal(0, Assert.Single(telemetries).AsType<Order>().OrderNumber);
        }

        // T040 - R29 option A (quickstart 15): the wrapper added as a telemetry type that is not enumerated, its
        // policy copying the member onto its parent, reaches the value
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForADetailsTelemetryCopyingOntoItsOrder_ItShould_SetTheOrderNumber(EntryPoint entryPoint)
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
                OnComplete = (telemetry, context) =>
                {
                    context.Parent(telemetry)!.Set(nameof(Order.OrderNumber), telemetry.Telemetry.AsType<Details>().OrderNumber);
                    telemetry.Enumerate(false);
                }
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(5, Assert.Single(telemetries).AsType<Order>().OrderNumber);
        }

        // T040 - R29 (quickstart 16): a wrapper with meaning, added as a telemetry type that is not enumerated,
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
                OnComplete = (telemetry, context) =>
                {
                    Measurement measurement = telemetry.Telemetry.AsType<Measurement>();

                    if (measurement.Unit == "kg")
                    {
                        context.Parent(telemetry)!.Set(nameof(Line.WeightAmount), measurement.Amount);
                    }

                    telemetry.Enumerate(false);
                }
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(3m, Assert.Single(telemetries).AsType<Line>().WeightAmount);
        }
    }
}
