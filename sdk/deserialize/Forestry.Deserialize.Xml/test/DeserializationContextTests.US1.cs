using System.Text;
using Forestry.Deserialize.Reading;
using Forestry.Deserialize.Xml.Tests.Fixtures;
using Forestry.Xml;
using Xunit;

namespace Forestry.Deserialize.Xml.Tests
{
    /// <summary>
    /// Spec 002, User Story 1: contextual rows and a deserialization context (tasks.md T002-T015;
    /// quickstart 1-7, 12, 13)
    /// </summary>
    /// <remarks>
    /// Test shells written before any code (C4), so they do not compile until T019-T027 declare the
    /// surface they assume (research R20-R26, R28, contracts/public-api.md). For the review gate (T018),
    /// the assumed surface, in namespace <c>Forestry.Deserialize.Reading</c>:
    /// <list type="bullet">
    /// <item><c>DeserializationContext</c>: <c>Rows&lt;TRow&gt;()</c> (open and kept rows of a type, oldest
    /// first), <c>Ancestors(ContextRow)</c> (open rows around it, innermost first), <c>Release(ContextRow)</c>.</item>
    /// <item><c>ContextRow</c>: <c>Row</c>, <c>IsOpen</c>, <c>IsSet(string memberName)</c>,
    /// <c>Set&lt;TValue&gt;(string memberName, TValue value)</c>, and the completion actions
    /// <c>Enumerate(bool)</c> and <c>Keep(Lifetime)</c>, each at most once.</item>
    /// <item><c>Lifetime</c>: <c>Leave</c> (default), <c>UntilReleased</c>, <c>UntilEnd</c>.</item>
    /// <item><c>EnumerationPolicy</c> methods taking the context: <c>Set&lt;T&gt;(ContextRow, ...)</c>,
    /// <c>Complete(ContextRow, DeserializationContext, Action next)</c> (replacing 001's <c>Enumerate</c>),
    /// <c>Clear(DeserializationContext)</c>.</item>
    /// <item><c>DeserializeOptions.MaxContextRows</c>: kept rows allowed, default 100; going past it throws
    /// an exception deriving from <see cref="InvalidOperationException"/> (type name Deferred).</item>
    /// <item>Misuse of an action (twice, at the wrong moment, on the wrong row) throws
    /// <see cref="InvalidOperationException"/>; an unknown member name throws <see cref="ArgumentException"/>.</item>
    /// </list>
    /// Policies are <see cref="ContextPolicy"/> instances whose hooks each test writes in place. Theories run
    /// both entry points (as in spec 001). US1-10 (a second enumeration while one runs throws) is spec 001's
    /// T091 test in <c>WalkingSkeletonTests.US4.cs</c>, kept as is.
    /// </remarks>
    public sealed class DeserializationContextTestsUS1
    {
        private static DeserializeXmlOptions OptionsWith(params Type[] rowTypes)
        {
            DeserializeXmlOptions options = new();
            options.Rows.Add(rowTypes);
            return options;
        }

        /// <summary>
        /// Read every row, calling <paramref name="onEach"/> on each as it is handed out, before the next is read
        /// </summary>
        private static List<Row> ReadEach(EntryPoint entryPoint, string document, DeserializeOptions options, Action<Row> onEach)
        {
            IEnumerable<Row> rows = entryPoint switch
            {
                EntryPoint.Sync => Deserialization.Deserialize(document, options),
                EntryPoint.Async => Deserialization.DeserializeAsync(new MemoryStream(Encoding.UTF8.GetBytes(document)), options).ToBlockingEnumerable(),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint))
            };

            List<Row> read = [];

            foreach (Row row in rows)
            {
                onEach(row);
                read.Add(row);
            }

            return read;
        }

        /// <summary>
        /// Read the first row, then stop the enumeration
        /// </summary>
        private static async Task ReadFirstRowThenStopAsync(EntryPoint entryPoint, string document, DeserializeOptions options)
        {
            if (entryPoint == EntryPoint.Sync)
            {
                using IEnumerator<Row> rows = Deserialization.Deserialize(document, options).GetEnumerator();
                Assert.True(rows.MoveNext());
            }
            else
            {
                await using IAsyncEnumerator<Row> rows = Deserialization.DeserializeAsync(new MemoryStream(Encoding.UTF8.GetBytes(document)), options).GetAsyncEnumerator();
                Assert.True(await rows.MoveNextAsync());
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
                OnComplete = (row, context) =>
                {
                    row.Enumerate(false);
                    row.Keep(Lifetime.UntilEnd);
                }
            });
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (row, context) =>
                {
                    Site site = context.Rows<Site>().Single().Row.AsType<Site>();
                    row.Set(nameof(Order.SiteKey), site.SiteKey);
                }
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.All(rows, row => Assert.True(row.Is<Order>()));
            Assert.Equal([1, 2], rows.Select(row => row.AsType<Order>().OrderNumber));
            Assert.Equal([7, 7], rows.Select(row => row.AsType<Order>().SiteKey));
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
                OnComplete = (row, context) =>
                {
                    ContextRow site = context.Ancestors(row).First();
                    site.Set(nameof(Site.ContactName), row.Row.AsType<Contact>().Name);
                    row.Enumerate(false);
                }
            });
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (row, context) => contactsSeenByOrder = context.Rows<Contact>().Count()
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(0, contactsSeenByOrder);
            Assert.Equal(2, rows.Count);
            Assert.True(rows[0].Is<Order>());
            Assert.Equal("Anna", rows[1].AsType<Site>().ContactName);
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
                OnComplete = (row, context) =>
                {
                    ContextRow around = context.Ancestors(row).First();
                    seenByLines.Add((around.Row.Is<Order>(), around.IsOpen));
                }
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([(true, true), (true, true)], seenByLines);
            Assert.Equal(["Line", "Line", "Order"], rows.Select(row => row.GetType().Name));
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
                OnComplete = (row, context) =>
                {
                    if (row.Row.AsType<Order>().OrderNumber == 2)
                    {
                        closedOrdersSeenBySecond.AddRange(context.Rows<Order>()
                            .Where(other => other != row && !other.IsOpen)
                            .Select(other => other.Row.AsType<Order>().OrderNumber));
                    }

                    row.Keep(Lifetime.UntilEnd);
                }
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([1, 2], rows.Select(row => row.AsType<Order>().OrderNumber));
            Assert.Equal([1], closedOrdersSeenBySecond);
        }

        // T006 - US1-6 / SC-002 (quickstart 6): at the natural end, kept rows are still there when Clear
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
            DeserializationContext? cleared = null;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (row, context) => row.Keep(Lifetime.UntilEnd),
                OnClear = context =>
                {
                    keptWhenCleared.Add(context.Rows<Order>().Count());
                    cleared = context;
                }
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([2], keptWhenCleared);
            Assert.Empty(cleared!.Rows<Order>());
        }

        // T006 - US1-6 / SC-002: an enumeration stopped early clears once and empties the context
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public async Task Deserialize_StoppedAfterTheFirstRow_ItShould_ClearOnceThenEmptyTheContext(EntryPoint entryPoint)
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
            DeserializationContext? cleared = null;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (row, context) => row.Keep(Lifetime.UntilEnd),
                OnClear = context =>
                {
                    keptWhenCleared.Add(context.Rows<Order>().Count());
                    cleared = context;
                }
            });

            // Act
            await ReadFirstRowThenStopAsync(entryPoint, document, options);

            // Assert
            Assert.Equal([1], keptWhenCleared);
            Assert.Empty(cleared!.Rows<Order>());
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
            DeserializationContext? cleared = null;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (row, context) =>
                {
                    if (row.Row.AsType<Order>().OrderNumber == 2)
                    {
                        throw new InvalidOperationException("The second order fails.");
                    }

                    row.Keep(Lifetime.UntilEnd);
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
            Assert.Empty(cleared!.Rows<Order>());
        }

        // T006 - US1-6 / SC-002: a document cut off inside a row clears once and empties the context
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
            DeserializationContext? cleared = null;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (row, context) => row.Keep(Lifetime.UntilEnd),
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
            Assert.Empty(cleared!.Rows<Order>());
        }

        // T007 - US1-8 / FR-007 (quickstart 5): an open site's key is not set for the order before its
        // key node, and set for the order after it. A row only has what has been read so far: true of
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
                OnComplete = (row, context) =>
                    siteKeySetForOrders.Add(context.Rows<Site>().Single().IsSet(nameof(Site.SiteKey)))
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([false, true], siteKeySetForOrders);
        }

        // T007 - US1-9 / FR-007: a closed row whose document had no node for a member reports it never set
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
                OnComplete = (row, context) =>
                {
                    if (row.Row.AsType<Order>().OrderNumber == 2)
                    {
                        ContextRow first = context.Rows<Order>().First();
                        firstSeenBySecond.Add((first.IsOpen, first.IsSet(nameof(Order.OrderNumber)), first.IsSet(nameof(Order.Note))));
                    }

                    row.Keep(Lifetime.UntilEnd);
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
                OnComplete = (row, context) => noteSet.Add(row.IsSet(nameof(Order.Note)))
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([true], noteSet);
            Assert.Null(Assert.Single(rows).AsType<Order>().Note);
        }

        // T007 - FR-007 / Q2: a member a policy sets through the context row counts as set
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
                OnComplete = (row, context) =>
                {
                    bool before = row.IsSet(nameof(Order.SiteKey));
                    row.Set(nameof(Order.SiteKey), 7);
                    siteKeySet.Add((before, row.IsSet(nameof(Order.SiteKey))));
                }
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([(false, true)], siteKeySet);
            Assert.Equal(7, Assert.Single(rows).AsType<Order>().SiteKey);
        }

        // T008 - FR-007 / R22: set members are tracked for types with more than 64 members
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForARowWithMoreThan64Members_ItShould_ReportExactlyTheSetOnes(EntryPoint entryPoint)
        {
            // Arrange
            const string document = """
                <Root>
                  <WideRow>
                    <M00>0</M00>
                    <M63>63</M63>
                    <M64>64</M64>
                    <M69>69</M69>
                  </WideRow>
                </Root>
                """;
            List<string> setMembers = [];
            DeserializeXmlOptions options = OptionsWith(typeof(WideRow));
            options.Policies.Add<WideRow>(new ContextPolicy
            {
                OnComplete = (row, context) => setMembers.AddRange(
                    Enumerable.Range(0, 70).Select(i => $"M{i:00}").Where(row.IsSet))
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(["M00", "M63", "M64", "M69"], setMembers);
        }

        // T008 - FR-007: asking about a member the row type does not have throws, naming it and the type
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
                OnComplete = (row, context) => row.IsSet("NoSuchMember")
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
                OnComplete = (row, context) =>
                {
                    if (row.Row.AsType<Order>().OrderNumber == 2)
                    {
                        firstNoteSeenBySecond.Add(context.Rows<Order>().First().Row.AsType<Order>().Note);
                    }

                    row.Keep(Lifetime.UntilEnd);
                }
            });

            // Act
            ReadEach(entryPoint, document, options, row => row.AsType<Order>().Note = "changed by the consumer");

            // Assert
            Assert.Equal(["original"], firstNoteSeenBySecond);
        }

        // T009 - US1-12 / FR-005 (quickstart 4): a policy setting a member of a kept order that already came
        // out throws, naming the type and the member: a row that came out no longer changes
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
                OnComplete = (row, context) =>
                {
                    if (row.Row.AsType<Order>().OrderNumber == 2)
                    {
                        context.Rows<Order>().First().Set(nameof(Order.Note), "updated by a policy");
                    }

                    row.Keep(Lifetime.UntilEnd);
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
                OnComplete = (row, context) =>
                {
                    if (row.Row.AsType<Order>().OrderNumber == 1)
                    {
                        row.Enumerate(false);
                        row.Keep(Lifetime.UntilEnd);
                    }
                    else
                    {
                        ContextRow first = context.Rows<Order>().First();
                        first.Set(nameof(Order.Note), "updated by a policy");
                        firstNoteSeenBySecond.Add(first.Row.AsType<Order>().Note);
                    }
                }
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(["updated by a policy"], firstNoteSeenBySecond);
            Assert.Equal([2], rows.Select(row => row.AsType<Order>().OrderNumber));
        }

        // T009 - US1-13 / FR-004: a line enumerated and not kept has left the context before the next row
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
                OnComplete = (row, context) => otherLinesSeen.Add(context.Rows<Line>().Count(other => other != row))
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(2, rows.Count);
            Assert.Equal([0, 0], otherLinesSeen);
        }

        // T010 - FR-003a (quickstart 12): two policies both deciding whether a row is enumerated throw on
        // the second, naming the row type and the action
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForTwoPoliciesEnumeratingOneRow_ItShould_ThrowOnTheSecond(EntryPoint entryPoint)
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
                .Add<Order>(new ContextPolicy { OnComplete = (row, context) => row.Enumerate(true) })
                .Add<Order>(new ContextPolicy { OnComplete = (row, context) => row.Enumerate(false) });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains(nameof(Order), exception.Message);
            Assert.Contains("Enumerate", exception.Message);
        }

        // T010 - FR-003a: two policies both setting a row's lifetime throw on the second
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForTwoPoliciesKeepingOneRow_ItShould_ThrowOnTheSecond(EntryPoint entryPoint)
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
                .Add<Order>(new ContextPolicy { OnComplete = (row, context) => row.Keep(Lifetime.UntilEnd) })
                .Add<Order>(new ContextPolicy { OnComplete = (row, context) => row.Keep(Lifetime.UntilReleased) });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains(nameof(Order), exception.Message);
            Assert.Contains("Keep", exception.Message);
        }

        // T010 - FR-003a: deciding whether a row is enumerated outside its completion throws
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
                OnSet = (row, member, value, context) => row.Enumerate(false)
            });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains("Enumerate", exception.Message);
        }

        // T010 - FR-003a: a child's policy deciding whether its open parent is enumerated throws: only the
        // row being completed decides, during its own completion
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
                OnComplete = (row, context) => context.Ancestors(row).First().Enumerate(false)
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
                OnComplete = (row, context) => context.Ancestors(row).First().Keep(Lifetime.UntilEnd)
            });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains(nameof(Order), exception.Message);
            Assert.Contains("Keep", exception.Message);
        }

        // T010 - FR-003a: a later policy setting the lifetime of a kept row whose completion is over throws
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
                OnComplete = (row, context) =>
                {
                    if (row.Row.AsType<Order>().OrderNumber == 1)
                    {
                        row.Keep(Lifetime.UntilReleased);
                    }
                    else
                    {
                        context.Rows<Order>().First().Keep(Lifetime.UntilEnd);
                    }
                }
            });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains(nameof(Order), exception.Message);
            Assert.Contains("Keep", exception.Message);
        }

        // T010 - FR-003a: releasing a kept row twice throws on the second
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
                OnComplete = (row, context) =>
                {
                    if (row.Row.AsType<Order>().OrderNumber == 2)
                    {
                        ContextRow first = context.Rows<Order>().First();
                        context.Release(first);
                        context.Release(first);
                    }

                    row.Keep(Lifetime.UntilReleased);
                }
            });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains("Release", exception.Message);
        }

        // T010 - FR-003a: releasing a row that is still open throws
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
                OnComplete = (row, context) => context.Release(context.Ancestors(row).First())
            });

            // Act
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains("Release", exception.Message);
        }

        // T010 - FR-003a: releasing a row kept until the end throws
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
                OnComplete = (row, context) =>
                {
                    if (row.Row.AsType<Order>().OrderNumber == 2)
                    {
                        context.Release(context.Rows<Order>().First());
                    }

                    row.Keep(Lifetime.UntilEnd);
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
                OnSet = (row, member, value, context) =>
                {
                    if (member.Name == nameof(Order.Note))
                    {
                        noteSetBeforeEachNote.Add(row.IsSet(nameof(Order.Note)));
                    }
                }
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([false, true], noteSetBeforeEachNote);
            Assert.Equal("second", Assert.Single(rows).AsType<Order>().Note);
        }

        // T012 - FR-003 / Q3: without policies a row comes out and leaves the context at its completion
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
                OnComplete = (row, context) => closedOrdersSeenByLine.Add(context.Rows<Order>().Count(order => !order.IsOpen))
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(["Order", "Line", "Order"], rows.Select(row => row.GetType().Name));
            Assert.Equal([0], closedOrdersSeenByLine);
        }

        // T012 - FR-003 / Q3: a policy that only keeps a row leaves "comes out" to the default
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
                OnComplete = (row, context) => row.Keep(Lifetime.UntilReleased)
            });
            options.Policies.Add<Line>(new ContextPolicy
            {
                OnComplete = (row, context) => closedOrdersSeenByLine.Add(context.Rows<Order>().Count(order => !order.IsOpen))
            });

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(["Order", "Line", "Order"], rows.Select(row => row.GetType().Name));
            Assert.Equal([1], closedOrdersSeenByLine);
        }

        // T013 - edge case: asking for a row type never kept gives nothing, not an error
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForRowsOfATypeNeverKept_ItShould_GiveAnEmptySequence(EntryPoint entryPoint)
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
                OnComplete = (row, context) => contactsSeen.Add(context.Rows<Contact>().Count())
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([0], contactsSeen);
        }

        // T013 - edge case / FR-005: a row released by a later policy is not seen by queries after it
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
                OnComplete = (row, context) =>
                {
                    int number = row.Row.AsType<Order>().OrderNumber;

                    if (number == 1)
                    {
                        row.Keep(Lifetime.UntilReleased);
                    }
                    else if (number == 2)
                    {
                        context.Release(context.Rows<Order>().First());
                    }
                    else
                    {
                        keptSeenByThird.AddRange(context.Rows<Order>().Where(other => other != row).Select(other => other.Row.AsType<Order>().OrderNumber));
                    }
                }
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Empty(keptSeenByThird);
        }

        // T013 - FR-010: releasing rows while iterating a query does not change that iteration, only later ones
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
                OnComplete = (row, context) =>
                {
                    int number = row.Row.AsType<Order>().OrderNumber;

                    if (number <= 2)
                    {
                        row.Keep(Lifetime.UntilReleased);
                    }
                    else if (number == 3)
                    {
                        foreach (ContextRow other in context.Rows<Order>().Where(other => other != row))
                        {
                            releasedByThird.Add(other.Row.AsType<Order>().OrderNumber);
                            context.Release(other);
                        }
                    }
                    else
                    {
                        keptSeenByFourth.AddRange(context.Rows<Order>().Where(other => other != row).Select(other => other.Row.AsType<Order>().OrderNumber));
                    }
                }
            });

            // Act
            EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([1, 2], releasedByThird);
            Assert.Empty(keptSeenByFourth);
        }

        // T014 - FR-011a (quickstart 13): keeping more rows than MaxContextRows throws, explaining the limit,
        // the kept rows per type and how to raise it; Clear runs once and the context is emptied
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForMoreKeptRowsThanTheLimit_ItShould_ThrowExplainingIt(EntryPoint entryPoint)
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
            DeserializationContext? cleared = null;
            DeserializeXmlOptions options = OptionsWith(typeof(Order));
            options.MaxContextRows = 3;
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (row, context) => row.Keep(Lifetime.UntilEnd),
                OnClear = context =>
                {
                    clears++;
                    cleared = context;
                }
            });

            // Act
            InvalidOperationException exception = Assert.ThrowsAny<InvalidOperationException>(() => EntryPoints.Read(entryPoint, document, options));

            // Assert
            Assert.Contains("MaxContextRows", exception.Message);
            Assert.Contains("3", exception.Message);
            Assert.Contains(nameof(Order), exception.Message);
            Assert.Equal(1, clears);
            Assert.Empty(cleared!.Rows<Order>());
        }

        // T014 - FR-011a: open rows do not count towards the limit
        [Theory]
        [InlineData(EntryPoint.Sync)]
        [InlineData(EntryPoint.Async)]
        public void Deserialize_ForMoreOpenRowsThanTheLimit_ItShould_NotThrow(EntryPoint entryPoint)
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
            options.MaxContextRows = 1;

            // Act
            List<Row> rows = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal(["Line", "Order", "Site"], rows.Select(row => row.GetType().Name));
        }

        // T015 - SC-003 (quickstart 7): the same options give the same rows in two enumerations one after the
        // other, the second seeing nothing kept by the first and reusing no row object from it (a row the
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
                OnComplete = (row, context) => row.Keep(Lifetime.UntilEnd)
            });
            options.Policies.Add<Order>(new ContextPolicy
            {
                OnComplete = (row, context) => sitesSeenByOrders.Add(context.Rows<Site>().Count())
            });

            // Act
            List<Row> first = EntryPoints.Read(entryPoint, document, options);
            List<Row> second = EntryPoints.Read(entryPoint, document, options);

            // Assert
            Assert.Equal([1, 1], sitesSeenByOrders);
            Assert.Equal(first.Select(row => row.GetType().Name), second.Select(row => row.GetType().Name));
            Assert.Equal(first[0].AsType<Order>().OrderNumber, second[0].AsType<Order>().OrderNumber);
            Assert.NotSame(first[0], second[0]);
        }
    }
}
