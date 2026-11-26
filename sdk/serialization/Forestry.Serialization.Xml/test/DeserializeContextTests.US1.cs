using System.Text;
using Forestry.Serialization.Reading;
using Forestry.Serialization.Xml.Tests.Fixtures;
using Forestry.Xml;
using Xunit;

namespace Forestry.Serialization.Xml.Tests
{
    /// <summary>
    /// Spec 002, User Story 1: contextual telemetries and a deserialization context (tasks.md T002-T015;
    /// quickstart 1-7, 12, 13)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4), so they do not compile until T019-T027 declare the
    /// surface they assume (research R20-R26, R28, contracts/public-api.md). For the review gate (T018),
    /// the assumed surface, in namespace <c>Forestry.Serialization.Reading</c>:
    /// <list type="bullet">
    /// <item><c>DeserializeContext</c>: <c>Telemetries&lt;TTelemetry&gt;()</c> (open and kept telemetries of a type, oldest
    /// first), <c>Ancestors(ContextTelemetry)</c> (open telemetries around it, innermost first), <c>Release(ContextTelemetry)</c>.</item>
    /// <item><c>ContextTelemetry</c>: <c>Telemetry</c>, <c>IsOpen</c>, <c>IsSet(string memberName)</c>,
    /// <c>Set&lt;TValue&gt;(string memberName, TValue value)</c>, and the completion actions
    /// <c>Enumerate(bool)</c> and <c>Keep(Lifetime)</c>, each at most once.</item>
    /// <item><c>Lifetime</c>: <c>Leave</c> (default), <c>UntilReleased</c>, <c>UntilEnd</c>.</item>
    /// <item><c>EnumerationPolicy</c> methods taking the context: <c>Set&lt;T&gt;(ContextTelemetry, ...)</c>,
    /// <c>Complete(ContextTelemetry, DeserializeContext, Action next)</c> (replacing 001's <c>Enumerate</c>),
    /// <c>Clear(DeserializeContext)</c>.</item>
    /// <item><c>DeserializeOptions.MaxContextTelemetries</c>: kept telemetries allowed, default 100; going past it throws
    /// an exception deriving from <see cref="InvalidOperationException"/> (type name Deferred).</item>
    /// <item>Misuse of an action (twice, at the wrong moment, on the wrong telemetry) throws
    /// <see cref="InvalidOperationException"/>; an unknown member name throws <see cref="ArgumentException"/>.</item>
    /// </list>
    /// Policies are <see cref="ContextPolicy"/> instances whose hooks each test writes in place. Theories run
    /// both entry points (as in spec 001). US1-10 (a second enumeration while one runs throws) is spec 001's
    /// T091 test in <c>WalkingSkeletonTests.US4.cs</c>, kept as is.
    /// </remarks>
    public sealed class DeserializeContextTestsUS1
    {
        private static DeserializeXmlOptions OptionsWith(params Type[] telemetryTypes)
        {
            DeserializeXmlOptions options = new();
            options.Telemetries.Add(telemetryTypes);
            return options;
        }

        /// <summary>
        /// Read every telemetry, calling <paramref name="onEach"/> on each as it is handed out, before the next is read
        /// </summary>
        private static List<Telemetry> ReadEach(EntryPoint entryPoint, string document, DeserializeOptions options, Action<Telemetry> onEach)
        {
            IEnumerable<Telemetry> telemetries = entryPoint switch
            {
                EntryPoint.Sync => TelemetrySerialization.Deserialize(document, options),
                EntryPoint.Async => TelemetrySerialization.DeserializeAsync(new MemoryStream(Encoding.UTF8.GetBytes(document)), options).ToBlockingEnumerable(),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint))
            };

            List<Telemetry> read = [];

            foreach (Telemetry telemetry in telemetries)
            {
                onEach(telemetry);
                read.Add(telemetry);
            }

            return read;
        }

        /// <summary>
        /// Read the first telemetry, then stop the enumeration
        /// </summary>
        private static async Task ReadFirstTelemetryThenStopAsync(EntryPoint entryPoint, string document, DeserializeOptions options)
        {
            if (entryPoint == EntryPoint.Sync)
            {
                using IEnumerator<Telemetry> telemetries = TelemetrySerialization.Deserialize(document, options).GetEnumerator();
                Assert.True(telemetries.MoveNext());
            }
            else
            {
                await using IAsyncEnumerator<Telemetry> telemetries = TelemetrySerialization.DeserializeAsync(new MemoryStream(Encoding.UTF8.GetBytes(document)), options).GetAsyncEnumerator();
                Assert.True(await telemetries.MoveNextAsync());
            }
        }

        // T002 - US1-1 / US1-2 / SC-001 (quickstart 1): a site kept and not enumerated; a policy on Order
        // copies its key from the context onto each order
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForASiteKeptAndNotEnumerated_ItShould_GiveOrdersCarryingItsKey(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Site>
                    <SiteKey>7</SiteKey>
                    <Order>
                      <OrderNumber>1</OrderNumber>
                    </Order>
                    <Order>
                      <OrderNumber>2</OrderNumber>
                    </Order>
                  </Site>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith(typeof(Site), typeof(Order));
            options.Policies.Add<Site>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    telemetry.Enumerate(false);
                    telemetry.Keep(Lifetime.UntilEnd);
                }
            });
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    Site site = context.Telemetries<Site>().Single().Telemetry.AsType<Site>();
                    telemetry.Set(nameof(Order.SiteKey), site.SiteKey);
                }
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.All(telemetries, telemetry => Assert.True(telemetry.Is<Order>()));
            Assert.Equal([1, 2], telemetries.Select(telemetry => telemetry.AsType<Order>().OrderNumber));
            Assert.Equal([7, 7], telemetries.Select(telemetry => telemetry.AsType<Order>().SiteKey));
        }

        // T003 - US1-3 (quickstart 2): a contact copies its name onto the open site, is not enumerated and
        // not kept; a later order's policy no longer finds it
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAContactCopiedOntoItsSite_ItShould_LeaveTheContextAtItsCompletion(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Site>
                    <SiteKey>7</SiteKey>
                    <Contact>
                      <Name>Anna</Name>
                    </Contact>
                    <Order>
                      <OrderNumber>1</OrderNumber>
                    </Order>
                  </Site>
                </Root>
                """;
            int? contactsSeenByOrder = null;
            DeserializeXmlOptions options = OptionsWith(typeof(Site), typeof(Contact), typeof(Order));
            options.Policies.Add<Contact>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    ContextTelemetry site = context.Ancestors(telemetry).First();
                    site.Set(nameof(Site.ContactName), telemetry.Telemetry.AsType<Contact>().Name);
                    telemetry.Enumerate(false);
                }
            });
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => contactsSeenByOrder = context.Telemetries<Contact>().Count()
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(0, contactsSeenByOrder);
            Assert.Equal(2, telemetries.Count);
            Assert.True(telemetries[0].Is<Order>());
            Assert.Equal("Anna", telemetries[1].AsType<Site>().ContactName);
        }

        // T004 - US1-4 / US1-7 (quickstart 3): a line's policy finds its order open around it; the order
        // comes out after its lines
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForLinesInsideAnOrder_ItShould_SeeTheOrderOpenAndGiveItAfterItsLines(EntryPoint entryPoint)
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
            List<(bool IsOrder, bool IsOpen)> seenByLines = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order), typeof(Line));
            options.Policies.Add<Line>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    ContextTelemetry around = context.Ancestors(telemetry).First();
                    seenByLines.Add((around.Telemetry.Is<Order>(), around.IsOpen));
                }
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([(true, true), (true, true)], seenByLines);
            Assert.Equal(["Line", "Line", "Order"], telemetries.Select(telemetry => telemetry.GetType().Name));
        }

        // T005 - US1-5: an order enumerated and kept is found, closed, by a later order's policy
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnOrderEnumeratedAndKept_ItShould_BeFoundClosedByALaterPolicy(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                </Root>
                """;
            List<int> closedOrdersSeenBySecond = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    if (telemetry.Telemetry.AsType<Order>().OrderNumber == 2)
                    {
                        closedOrdersSeenBySecond.AddRange(context.Telemetries<Order>()
                            .Where(other => other != telemetry && !other.IsOpen)
                            .Select(other => other.Telemetry.AsType<Order>().OrderNumber));
                    }

                    telemetry.Keep(Lifetime.UntilEnd);
                }
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([1, 2], telemetries.Select(telemetry => telemetry.AsType<Order>().OrderNumber));
            Assert.Equal([1], closedOrdersSeenBySecond);
        }

        // T006 - US1-6 / SC-002 (quickstart 6): at the natural end, kept telemetries are still there when Clear
        // runs, once, and the context is empty afterwards
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ToTheEnd_ItShould_ClearOnceThenEmptyTheContext(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                </Root>
                """;
            List<int> keptWhenCleared = [];
            DeserializeContext? cleared = null;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => telemetry.Keep(Lifetime.UntilEnd),
                OnClear = context =>
                {
                    keptWhenCleared.Add(context.Telemetries<Order>().Count());
                    cleared = context;
                }
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([2], keptWhenCleared);
            Assert.Empty(cleared!.Telemetries<Order>());
        }

        // T006 - US1-6 / SC-002: an enumeration stopped early clears once and empties the context
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public async Task Deserialize_StoppedAfterTheFirstTelemetry_ItShould_ClearOnceThenEmptyTheContext(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                </Root>
                """;
            List<int> keptWhenCleared = [];
            DeserializeContext? cleared = null;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => telemetry.Keep(Lifetime.UntilEnd),
                OnClear = context =>
                {
                    keptWhenCleared.Add(context.Telemetries<Order>().Count());
                    cleared = context;
                }
            });

            // Act
            await ReadFirstTelemetryThenStopAsync(entryPoint, document, options);

            // Assert
            Assert.Equal([1], keptWhenCleared);
            Assert.Empty(cleared!.Telemetries<Order>());
        }

        // T006 - US1-6 / SC-002: an enumeration ended by a throwing policy clears once and empties the context
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAPolicyThatThrows_ItShould_ClearOnceThenEmptyTheContext(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                </Root>
                """;
            int clears = 0;
            DeserializeContext? cleared = null;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    if (telemetry.Telemetry.AsType<Order>().OrderNumber == 2)
                    {
                        throw new InvalidOperationException("The second order fails.");
                    }

                    telemetry.Keep(Lifetime.UntilEnd);
                },
                OnClear = context =>
                {
                    clears++;
                    cleared = context;
                }
            });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Equal("The second order fails.", exception.Message);
            Assert.Equal(1, clears);
            Assert.Empty(cleared!.Telemetries<Order>());
        }

        // T006 - US1-6 / SC-002: a document cut off inside a telemetry clears once and empties the context
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForADocumentCutOffInsideAnOrder_ItShould_ClearOnceThenEmptyTheContext(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                """;
            int clears = 0;
            DeserializeContext? cleared = null;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => telemetry.Keep(Lifetime.UntilEnd),
                OnClear = context =>
                {
                    clears++;
                    cleared = context;
                }
            });

            // Act
            Assert.Throws<XmlException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Equal(1, clears);
            Assert.Empty(cleared!.Telemetries<Order>());
        }

        // T007 - US1-8 / FR-007 (quickstart 5): an open site's key is not set for the order before its
        // key node, and set for the order after it. A telemetry only has what has been read so far: true of
        // any forward-only read (C2), not just of XML (spec Assumptions)
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForASiteKeyAfterTheFirstOrder_ItShould_BeSetOnlyForLaterOrders(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Site>
                    <Order>
                      <OrderNumber>1</OrderNumber>
                    </Order>
                    <SiteKey>7</SiteKey>
                    <Order>
                      <OrderNumber>2</OrderNumber>
                    </Order>
                  </Site>
                </Root>
                """;
            List<bool> siteKeySetForOrders = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Site), typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                    siteKeySetForOrders.Add(context.Telemetries<Site>().Single().IsSet(nameof(Site.SiteKey)))
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([false, true], siteKeySetForOrders);
        }

        // T007 - US1-9 / FR-007: a closed telemetry whose document had no node for a member reports it never set
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAClosedOrderWithoutANote_ItShould_ReportTheNoteNotSet(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                </Root>
                """;
            List<(bool IsOpen, bool NumberSet, bool NoteSet)> firstSeenBySecond = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    if (telemetry.Telemetry.AsType<Order>().OrderNumber == 2)
                    {
                        ContextTelemetry first = context.Telemetries<Order>().First();
                        firstSeenBySecond.Add((first.IsOpen, first.IsSet(nameof(Order.OrderNumber)), first.IsSet(nameof(Order.Note))));
                    }

                    telemetry.Keep(Lifetime.UntilEnd);
                }
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([(false, true, false)], firstSeenBySecond);
        }

        // T007 - FR-007: a member set by an empty node counts as set
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnEmptyNoteNode_ItShould_ReportTheNoteSet(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                    <Note />
                  </Order>
                </Root>
                """;
            List<bool> noteSet = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => noteSet.Add(telemetry.IsSet(nameof(Order.Note)))
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([true], noteSet);
            Assert.Null(Assert.Single(telemetries).AsType<Order>().Note);
        }

        // T007 - FR-007 / Q2: a member a policy sets through the context telemetry counts as set
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAMemberSetByAPolicy_ItShould_ReportItSet(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                </Root>
                """;
            List<(bool Before, bool After)> siteKeySet = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    bool before = telemetry.IsSet(nameof(Order.SiteKey));
                    telemetry.Set(nameof(Order.SiteKey), 7);
                    siteKeySet.Add((before, telemetry.IsSet(nameof(Order.SiteKey))));
                }
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([(false, true)], siteKeySet);
            Assert.Equal(7, Assert.Single(telemetries).AsType<Order>().SiteKey);
        }

        // T008 - FR-007 / R22: set members are tracked for types with more than 64 members
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForATelemetryWithMoreThan64Members_ItShould_ReportExactlyTheSetOnes(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <WideTelemetry>
                    <M00>0</M00>
                    <M63>63</M63>
                    <M64>64</M64>
                    <M69>69</M69>
                  </WideTelemetry>
                </Root>
                """;
            List<string> setMembers = [];
            DeserializeXmlOptions options = OptionsWith(typeof(WideTelemetry));
            options.Policies.Add<WideTelemetry>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => setMembers.AddRange(
                    Enumerable.Range(0, 70).Select(i => $"M{i:00}").Where(telemetry.IsSet))
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(["M00", "M63", "M64", "M69"], setMembers);
        }

        // T008 - FR-007: asking about a member the telemetry type does not have throws, naming it and the type
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForIsSetWithAnUnknownMember_ItShould_ThrowNamingItAndTheType(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => telemetry.IsSet("NoSuchMember")
            });

            // Act
            ArgumentException exception = Assert.ThrowsAny<ArgumentException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains("NoSuchMember", exception.Message);
            Assert.Contains(nameof(Order), exception.Message);
        }

        // T009 - US1-11 / FR-021 (quickstart 4): the consumer changing an order it received does not change
        // what a later policy reads from the context
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAConsumerChangingAKeptOrder_ItShould_LeaveTheContextsOrderUnchanged(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                    <Note>original</Note>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                </Root>
                """;
            List<string?> firstNoteSeenBySecond = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    if (telemetry.Telemetry.AsType<Order>().OrderNumber == 2)
                    {
                        firstNoteSeenBySecond.Add(context.Telemetries<Order>().First().Telemetry.AsType<Order>().Note);
                    }

                    telemetry.Keep(Lifetime.UntilEnd);
                }
            });

            // Act
            ReadEach(entryPoint, document, options, telemetry => telemetry.AsType<Order>().Note = "changed by the consumer");

            // Assert
            Assert.Equal(["original"], firstNoteSeenBySecond);
        }

        // T009 - US1-12 / FR-005 (quickstart 4): a policy setting a member of a kept order that already came
        // out throws, naming the type and the member: a telemetry that came out no longer changes
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAPolicyUpdatingAnOrderThatCameOut_ItShould_Throw(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                    <Note>original</Note>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    if (telemetry.Telemetry.AsType<Order>().OrderNumber == 2)
                    {
                        context.Telemetries<Order>().First().Set(nameof(Order.Note), "updated by a policy");
                    }

                    telemetry.Keep(Lifetime.UntilEnd);
                }
            });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains(nameof(Order), exception.Message);
            Assert.Contains(nameof(Order.Note), exception.Message);
        }

        // T009 - FR-005: a kept order that did not come out can still be updated by a later policy
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAPolicyUpdatingAKeptOrderThatDidNotComeOut_ItShould_SetTheMember(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                    <Note>original</Note>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                </Root>
                """;
            List<string?> firstNoteSeenBySecond = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    if (telemetry.Telemetry.AsType<Order>().OrderNumber == 1)
                    {
                        telemetry.Enumerate(false);
                        telemetry.Keep(Lifetime.UntilEnd);
                    }
                    else
                    {
                        ContextTelemetry first = context.Telemetries<Order>().First();
                        first.Set(nameof(Order.Note), "updated by a policy");
                        firstNoteSeenBySecond.Add(first.Telemetry.AsType<Order>().Note);
                    }
                }
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(["updated by a policy"], firstNoteSeenBySecond);
            Assert.Equal([2], telemetries.Select(telemetry => telemetry.AsType<Order>().OrderNumber));
        }

        // T009 - US1-13 / FR-004: a line enumerated and not kept has left the context before the next telemetry
        // is read
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForALineEnumeratedAndNotKept_ItShould_HaveLeftTheContext(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Line>
                    <LineNumber>1</LineNumber>
                  </Line>
                  <Line>
                    <LineNumber>2</LineNumber>
                  </Line>
                </Root>
                """;
            List<int> otherLinesSeen = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Line));
            options.Policies.Add<Line>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => otherLinesSeen.Add(context.Telemetries<Line>().Count(other => other != telemetry))
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(2, telemetries.Count);
            Assert.Equal([0, 0], otherLinesSeen);
        }

        // T010 - FR-003a (quickstart 12): two policies both deciding whether a telemetry is enumerated throw on
        // the second, naming the telemetry type and the action
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForTwoPoliciesEnumeratingOneTelemetry_ItShould_ThrowOnTheSecond(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies
                .Add<Order>(new ContextPolicy { OnComplete = (telemetry, context) => telemetry.Enumerate(true) })
                .Add<Order>(new ContextPolicy { OnComplete = (telemetry, context) => telemetry.Enumerate(false) });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains(nameof(Order), exception.Message);
            Assert.Contains("Enumerate", exception.Message);
        }

        // T010 - FR-003a: two policies both setting a telemetry's lifetime throw on the second
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForTwoPoliciesKeepingOneTelemetry_ItShould_ThrowOnTheSecond(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies
                .Add<Order>(new ContextPolicy { OnComplete = (telemetry, context) => telemetry.Keep(Lifetime.UntilEnd) })
                .Add<Order>(new ContextPolicy { OnComplete = (telemetry, context) => telemetry.Keep(Lifetime.UntilReleased) });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains(nameof(Order), exception.Message);
            Assert.Contains("Keep", exception.Message);
        }

        // T010 - FR-003a: deciding whether a telemetry is enumerated outside its completion throws
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForEnumerateCalledWhileAMemberIsSet_ItShould_Throw(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnSet = (telemetry, member, value, context) => telemetry.Enumerate(false)
            });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains("Enumerate", exception.Message);
        }

        // T010 - FR-003a: a child's policy deciding whether its open parent is enumerated throws: only the
        // telemetry being completed decides, during its own completion
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForEnumerateCalledOnAnOpenParent_ItShould_Throw(EntryPoint entryPoint)
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
            DeserializeXmlOptions options = OptionsWith(typeof(Order), typeof(Line));
            options.Policies.Add<Line>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => context.Ancestors(telemetry).First().Enumerate(false)
            });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains(nameof(Order), exception.Message);
            Assert.Contains("Enumerate", exception.Message);
        }

        // T010 - FR-003a: a child's policy setting its open parent's lifetime throws
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForKeepCalledOnAnOpenParent_ItShould_Throw(EntryPoint entryPoint)
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
            DeserializeXmlOptions options = OptionsWith(typeof(Order), typeof(Line));
            options.Policies.Add<Line>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => context.Ancestors(telemetry).First().Keep(Lifetime.UntilEnd)
            });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains(nameof(Order), exception.Message);
            Assert.Contains("Keep", exception.Message);
        }

        // T010 - FR-003a: a later policy setting the lifetime of a kept telemetry whose completion is over throws
        // (only Release is left for it)
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForKeepCalledOnAClosedKeptOrder_ItShould_Throw(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    if (telemetry.Telemetry.AsType<Order>().OrderNumber == 1)
                    {
                        telemetry.Keep(Lifetime.UntilReleased);
                    }
                    else
                    {
                        context.Telemetries<Order>().First().Keep(Lifetime.UntilEnd);
                    }
                }
            });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains(nameof(Order), exception.Message);
            Assert.Contains("Keep", exception.Message);
        }

        // T010 - FR-003a: releasing a kept telemetry twice throws on the second
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAKeptOrderReleasedTwice_ItShould_ThrowOnTheSecond(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    if (telemetry.Telemetry.AsType<Order>().OrderNumber == 2)
                    {
                        ContextTelemetry first = context.Telemetries<Order>().First();
                        context.Release(first);
                        context.Release(first);
                    }

                    telemetry.Keep(Lifetime.UntilReleased);
                }
            });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains("Release", exception.Message);
        }

        // T010 - FR-003a: releasing a telemetry that is still open throws
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnOpenOrderReleased_ItShould_Throw(EntryPoint entryPoint)
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
            DeserializeXmlOptions options = OptionsWith(typeof(Order), typeof(Line));
            options.Policies.Add<Line>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => context.Release(context.Ancestors(telemetry).First())
            });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains("Release", exception.Message);
        }

        // T010 - FR-003a: releasing a telemetry kept until the end throws
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnOrderKeptUntilTheEndReleased_ItShould_Throw(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                </Root>
                """;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    if (telemetry.Telemetry.AsType<Order>().OrderNumber == 2)
                    {
                        context.Release(context.Telemetries<Order>().First());
                    }

                    telemetry.Keep(Lifetime.UntilEnd);
                }
            });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains("Release", exception.Message);
        }

        // T011 - Q1 / FR-013: a repeated member node leaves the last value, and a set policy sees the member
        // already set before the second value
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForARepeatedNote_ItShould_KeepTheLastAndShowTheRepeatToASetPolicy(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                    <Note>first</Note>
                    <Note>second</Note>
                  </Order>
                </Root>
                """;
            List<bool> noteSetBeforeEachNote = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnSet = (telemetry, member, value, context) =>
                {
                    if (member.Name == nameof(Order.Note))
                    {
                        noteSetBeforeEachNote.Add(telemetry.IsSet(nameof(Order.Note)));
                    }
                }
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([false, true], noteSetBeforeEachNote);
            Assert.Equal("second", Assert.Single(telemetries).AsType<Order>().Note);
        }

        // T012 - FR-003 / Q3: without policies a telemetry comes out and leaves the context at its completion
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnOrderWithoutPolicy_ItShould_ComeOutAndLeaveTheContext(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                    <Line>
                      <LineNumber>1</LineNumber>
                    </Line>
                  </Order>
                </Root>
                """;
            List<int> closedOrdersSeenByLine = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order), typeof(Line));
            options.Policies.Add<Line>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => closedOrdersSeenByLine.Add(context.Telemetries<Order>().Count(order => !order.IsOpen))
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(["Order", "Line", "Order"], telemetries.Select(telemetry => telemetry.GetType().Name));
            Assert.Equal([0], closedOrdersSeenByLine);
        }

        // T012 - FR-003 / Q3: a policy that only keeps a telemetry leaves "comes out" to the default
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAnOrderOnlyKept_ItShould_StillComeOut(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                    <Line>
                      <LineNumber>1</LineNumber>
                    </Line>
                  </Order>
                </Root>
                """;
            List<int> closedOrdersSeenByLine = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order), typeof(Line));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => telemetry.Keep(Lifetime.UntilReleased)
            });
            options.Policies.Add<Line>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => closedOrdersSeenByLine.Add(context.Telemetries<Order>().Count(order => !order.IsOpen))
            });

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(["Order", "Line", "Order"], telemetries.Select(telemetry => telemetry.GetType().Name));
            Assert.Equal([1], closedOrdersSeenByLine);
        }

        // T013 - edge case: asking for a telemetry type never kept gives nothing, not an error
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForTelemetriesOfATypeNeverKept_ItShould_GiveAnEmptySequence(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                </Root>
                """;
            List<int> contactsSeen = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order), typeof(Contact));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => contactsSeen.Add(context.Telemetries<Contact>().Count())
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([0], contactsSeen);
        }

        // T013 - edge case / FR-005: a telemetry released by a later policy is not seen by queries after it
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForAKeptOrderReleasedLater_ItShould_NotBeSeenAfterwards(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>3</OrderNumber>
                  </Order>
                </Root>
                """;
            List<int> keptSeenByThird = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    int number = telemetry.Telemetry.AsType<Order>().OrderNumber;

                    if (number == 1)
                    {
                        telemetry.Keep(Lifetime.UntilReleased);
                    }
                    else if (number == 2)
                    {
                        context.Release(context.Telemetries<Order>().First());
                    }
                    else
                    {
                        keptSeenByThird.AddRange(context.Telemetries<Order>().Where(other => other != telemetry).Select(other => other.Telemetry.AsType<Order>().OrderNumber));
                    }
                }
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Empty(keptSeenByThird);
        }

        // T013 - FR-010: releasing telemetries while iterating a query does not change that iteration, only later ones
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForReleasesDuringAQuery_ItShould_TakeEffectAfterIt(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>3</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>4</OrderNumber>
                  </Order>
                </Root>
                """;
            List<int> releasedByThird = [];
            List<int> keptSeenByFourth = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) =>
                {
                    int number = telemetry.Telemetry.AsType<Order>().OrderNumber;

                    if (number <= 2)
                    {
                        telemetry.Keep(Lifetime.UntilReleased);
                    }
                    else if (number == 3)
                    {
                        foreach (ContextTelemetry other in context.Telemetries<Order>().Where(other => other != telemetry))
                        {
                            releasedByThird.Add(other.Telemetry.AsType<Order>().OrderNumber);
                            context.Release(other);
                        }
                    }
                    else
                    {
                        keptSeenByFourth.AddRange(context.Telemetries<Order>().Where(other => other != telemetry).Select(other => other.Telemetry.AsType<Order>().OrderNumber));
                    }
                }
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([1, 2], releasedByThird);
            Assert.Empty(keptSeenByFourth);
        }

        // T014 - FR-011a (quickstart 13): keeping more telemetries than MaxContextTelemetries throws, explaining the limit,
        // the kept telemetries per type and how to raise it; Clear runs once and the context is emptied
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForMoreKeptTelemetriesThanTheLimit_ItShould_ThrowExplainingIt(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Order>
                    <OrderNumber>1</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>2</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>3</OrderNumber>
                  </Order>
                  <Order>
                    <OrderNumber>4</OrderNumber>
                  </Order>
                </Root>
                """;
            int clears = 0;
            DeserializeContext? cleared = null;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.MaxContextTelemetries = 3;
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => telemetry.Keep(Lifetime.UntilEnd),
                OnClear = context =>
                {
                    clears++;
                    cleared = context;
                }
            });

            // Act
            InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains("MaxContextTelemetries", exception.Message);
            Assert.Contains("3", exception.Message);
            Assert.Contains(nameof(Order), exception.Message);
            Assert.Equal(1, clears);
            Assert.Empty(cleared!.Telemetries<Order>());
        }

        // T014 - FR-011a: open telemetries do not count towards the limit
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForMoreOpenTelemetriesThanTheLimit_ItShould_NotThrow(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
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
                """;
            DeserializeXmlOptions options = OptionsWith(typeof(Site), typeof(Order), typeof(Line));
            options.MaxContextTelemetries = 1;

            // Act
            List<Telemetry> telemetries = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(["Line", "Order", "Site"], telemetries.Select(telemetry => telemetry.GetType().Name));
        }

        // T015 - SC-003 (quickstart 7): the same options give the same telemetries in two enumerations one after the
        // other, the second seeing nothing kept by the first and reusing no telemetry object from it (a telemetry the
        // consumer still holds never changes under it)
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_TwiceWithTheSameOptions_ItShould_CarryNothingOver(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <Site>
                    <SiteKey>7</SiteKey>
                    <Order>
                      <OrderNumber>1</OrderNumber>
                    </Order>
                  </Site>
                </Root>
                """;
            List<int> sitesSeenByOrders = [];
            DeserializeXmlOptions options = OptionsWith(typeof(Site), typeof(Order));
            options.Policies.Add<Site>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => telemetry.Keep(Lifetime.UntilEnd)
            });
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (telemetry, context) => sitesSeenByOrders.Add(context.Telemetries<Site>().Count())
            });

            // Act
            List<Telemetry> first = EntryPoints.Read(entryPoint, document, options);
            List<Telemetry> second = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([1, 1], sitesSeenByOrders);
            Assert.Equal(first.Select(telemetry => telemetry.GetType().Name), second.Select(telemetry => telemetry.GetType().Name));
            Assert.Equal(first[0].AsType<Order>().OrderNumber, second[0].AsType<Order>().OrderNumber);
            Assert.NotSame(first[0], second[0]);
        }
    }
}
