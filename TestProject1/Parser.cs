using Alphashack.Graphdat.Agent.SqlQueryHelper;
using Gehtsoft.PCRE;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Alphashack.Graphdat.Agent.SqlQueryHelperTests
{
    /// <summary>
    /// Summary description for Parser
    /// </summary>
    [TestClass]
    public class Parser
    {
        public Parser()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void MatchesShouldReturnMinusOneWhenNotMatched()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Matches("test", "not");

            // Assert
            Assert.AreEqual(-1, result);
        }

        [TestMethod]
        public void MatchesShouldReturnIndexWhenMatched()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Matches("testblahtest", "blah");

            // Assert
            Assert.AreEqual(4, result);
        }

        [TestMethod]
        public void MatchingShouldReturnNullWhenNotMatched()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Matching("test", "not");

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void MatchingShouldReturnSubstringWhenMatched()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Matching("test blah test", "blah");

            // Assert
            Assert.AreEqual("blah", result);
        }

        [TestMethod]
        public void MatchingShouldReturnNullWhenGroupOutOfRange()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Matching("test blah test", "blah", group: 1);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void MatchingShouldReturnGroupWhenMatched()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Matching("test blah test", @".*(blah).*", group: 1);

            // Assert
            Assert.AreEqual("blah", result);
        }

        [TestMethod]
        public void SubstititeShouldReplaceSimpleMatches()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Substitute("test blah test blah test", @"blah", "something");

            // Assert
            Assert.AreEqual("test something test something test", result);
        }

        [TestMethod]
        public void SubstititeShouldReplaceSimpleMatchesWithGroup()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Substitute("test blah1blah test blah2blah test", @"blah(.)blah", "$1");

            // Assert
            Assert.AreEqual("test 1 test 2 test", result);
        }

        [TestMethod]
        public void SubstititeShouldReplaceSimpleMatchesWithGroupWithPadding()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Substitute("test xblah1blahx test xblah2blahx test", @"blah(.)blah", "#1");

            // Assert
            Assert.AreEqual("test x 1 x test x 2 x test", result);
        }

        [TestMethod]
        public void SubstititeShouldReplaceSimpleMatchesAfterOffset()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Substitute("test blah test blah test", @"blah", "something", offset: 10);

            // Assert
            Assert.AreEqual("test blah test something test", result);
        }

        [TestMethod]
        public void RewriteShouldRemoveStoredProcedureArguments()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result1 = subject.Rewrite("exec my_proc 1, 2, 3");
            var result2 = subject.Rewrite("execute my_proc 1, 2, 3");
            var result3 = subject.Rewrite("EXEC my_proc 1, 2, 3");
            var result4 = subject.Rewrite("EXECUTE my_proc 1, 2, 3");

            // Assert
            Assert.AreEqual("exec my_proc", result1);
            Assert.AreEqual("execute my_proc", result2);
            Assert.AreEqual("exec my_proc", result3);
            Assert.AreEqual("execute my_proc", result4);
        }

        [TestMethod]
        public void RewriteShouldRemoveSingleLineComments()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Rewrite(@"blah
-- comment
blah");

            // Assert
            Assert.AreEqual(@"blah blah", result);
        }

        [TestMethod]
        public void RewriteShouldRemoveMultipleLineComments()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Rewrite(@"blah
/* comment
comment
comment */
blah");

            // Assert
            Assert.AreEqual(@"blah blah", result);
        }

        [TestMethod]
        public void RewriteShouldGeneraliseUse()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Rewrite(@"use blahblah");
            var result1 = subject.Rewrite(@"USE blahblah");

            // Assert
            Assert.AreEqual(@"use ?", result);
            Assert.AreEqual(@"use ?", result1);
        }

        [TestMethod]
        public void RewriteShouldReplaceQuotedStrings()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Rewrite(@"one blah ""blah"" blah");
            var result1 = subject.Rewrite(@"two blah 'blah' blah");
            var result2 = subject.Rewrite(@"three blah ""bl\""ah"" blah");
            var result3 = subject.Rewrite(@"four blah 'bl\'ah' blah");

            // Assert
            Assert.AreEqual(@"one blah ? blah", result);
            Assert.AreEqual(@"two blah ? blah", result1);
            Assert.AreEqual(@"three blah ? blah", result2);
            Assert.AreEqual(@"four blah ? blah", result3);
        }

        [TestMethod]
        public void RewriteShouldReplaceMD5s()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Rewrite(@"blah 79054025255fb1a26e4bc422aef54eb4 blah");
            var result1 = subject.Rewrite(@"blah -79054025255fb1a26e4bc422aef54eb4 blah");
            var result2 = subject.Rewrite(@"blah _79054025255fb1a26e4bc422aef54eb4 blah");
            var result3 = subject.Rewrite(@"blah .79054025255fb1a26e4bc422aef54eb4 blah");
            var result4 = subject.Rewrite(@"blah x79054025255fb1a26e4bc422aef54eb4 blah");

            // Assert
            Assert.AreEqual(@"blah ? blah", result);
            Assert.AreEqual(@"blah ? blah", result1);
            Assert.AreEqual(@"blah ? blah", result2);
            Assert.AreEqual(@"blah ? blah", result3);
            Assert.AreEqual(@"blah x79054025255fb1a26e4bc422aef54eb4 blah", result4);
        }

        [TestMethod]
        public void RewriteShouldReplaceNumbers()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var resulta = subject.Rewrite(@"a blah 123 blah");
            var resultb = subject.Rewrite(@"b blah -123 blah");
            var resultc = subject.Rewrite(@"c blah 0xab blah");
            var resultd = subject.Rewrite(@"d blah 1.23 blah");
            var resulte = subject.Rewrite(@"e blah ab12 blah");

            // Assert
            Assert.AreEqual(@"a blah ? blah", resulta);
            Assert.AreEqual(@"b blah ? blah", resultb);
            Assert.AreEqual(@"c blah ? blah", resultc);
            Assert.AreEqual(@"d blah ? blah", resultd);
            Assert.AreEqual(@"e blah ab12 blah", resulte);
        }

        [TestMethod]
        public void RewriteShouldRemoveWhitespace()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Rewrite(@"
  blah");
            var result1 = subject.Rewrite(@"blah  

  ");
            var result2 = subject.Rewrite(@"blah   blah

blah");

            // Assert
            Assert.AreEqual(@"blah", result);
            Assert.AreEqual(@"blah", result1);
            Assert.AreEqual(@"blah blah blah", result2);
        }

        [TestMethod]
        public void RewriteShouldReplaceNull()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Rewrite(@"null");
            var result1 = subject.Rewrite(@"is not null");
            var result2 = subject.Rewrite(@"is null");

            // Assert
            Assert.AreEqual(@"?", result);
            Assert.AreEqual(@"?null?", result1);
            Assert.AreEqual(@"?null?", result2);
        }

        [TestMethod]
        public void RewriteShouldCollapeIn()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Rewrite(@"in (1, 2, 3, 4)");

            // Assert
            Assert.AreEqual(@"in (?)", result);
        }

        [TestMethod]
        public void RewriteShouldCollapeValues()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Rewrite(@"values (1, 2, 3, 4)");

            // Assert
            Assert.AreEqual(@"values (?)", result);
        }

        [TestMethod]
        public void RewriteShouldCollapeUnion()
        {
            // Arrange
            var subject = new SqlQueryHelper.Parser();

            // Act
            var result = subject.Rewrite(@"select a from b
union
select a from b
union all
select a from b
union
select b from c
union
select b from c
");

            // Assert
            Assert.AreEqual(@"select a from b union select b from c", result);
        }
    }
}
