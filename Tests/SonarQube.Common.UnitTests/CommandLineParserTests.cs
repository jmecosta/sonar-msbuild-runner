﻿//-----------------------------------------------------------------------
// <copyright file="CommandLineParserTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------


using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using TestUtilities;

namespace SonarQube.Common.UnitTests
{
    [TestClass]
    public class CommandLineParserTests
    {
        public TestContext TestContext { get; set; }

        #region Tests

        [TestMethod]
        public void Parser_InvalidArguments()
        {
            AssertException.Expects<ArgumentNullException>(() => new CommandLineParser(null, true));
        }

        [TestMethod]
        public void Parser_DuplicateDescriptorIds()
        {
            ArgumentDescriptor d1 = new ArgumentDescriptor("id1", new string[] { "a" }, true, "desc1", false);
            ArgumentDescriptor d2 = new ArgumentDescriptor("id1", new string[] { "b" }, true, "desc2", false);

            AssertException.Expects<ArgumentException>(() => new CommandLineParser(
                new ArgumentDescriptor[] { d1, d2 }, true));
        }

        [TestMethod]
        public void Parser_UnrecognizedArguments()
        {
            CommandLineParser parser;
            IEnumerable<ArgumentInstance> instances;
            TestLogger logger;

            string[] args = new string[] { "/a:XXX", "/unrecognized" };

            ArgumentDescriptor d1 = new ArgumentDescriptor("id1", new string[] { "/a:" }, true, "desc1", false);

            // 1. Don't allow unrecognized
            parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, false);

            logger = CheckProcessingFails(parser, args);

            logger.AssertSingleErrorExists("/unrecognized");
            logger.AssertErrorsLogged(1);

            // 2. Allow unrecognized
            parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, true);
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, args);

            AssertExpectedValue("id1", "XXX", instances);
            AssertExpectedInstancesCount(1, instances);
            logger.AssertMessagesLogged(0); // expecting unrecognized arguments to be ignored silently
        }

        [TestMethod]
        public void Parser_CaseSensitivity()
        {
            string[] args = new string[] { "aaa:all lowercase", "AAA:all uppercase", "aAa: mixed case" };

            ArgumentDescriptor d1 = new ArgumentDescriptor("id", new string[] { "AAA:" }, true/* allow multiples */ , "desc1", true /* allow multiple */);
            CommandLineParser parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, true /* allow unrecognized */);

            // Act
            IEnumerable<ArgumentInstance> instances = CheckProcessingSucceeds(parser, new TestLogger(), args);

            AssertExpectedValue("id", "all uppercase", instances);
            AssertExpectedInstancesCount(1, instances);
        }

        [TestMethod]
        public void Parser_Multiples()
        {
            CommandLineParser parser;
            IEnumerable<ArgumentInstance> instances;
            TestLogger logger;

            string[] args = new string[] { "zzzv1", "zzzv2", "zzzv3" };

            // 1. Don't allow multiples
            ArgumentDescriptor d1 = new ArgumentDescriptor("id", new string[] { "zzz" }, true, "desc1", false /* no multiples */);
            parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, false);

            logger = CheckProcessingFails(parser, args);

            logger.AssertSingleErrorExists("zzzv2", "v1");
            logger.AssertSingleErrorExists("zzzv3", "v1");
            logger.AssertErrorsLogged(2);


            // 2. Allow multiples
            d1 = new ArgumentDescriptor("id", new string[] { "zzz" }, true, "desc1", true /* allow multiple */);
            parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, true);
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, args);

            AssertExpectedValues("id", instances, "v1", "v2", "v3");
            AssertExpectedInstancesCount(3, instances);
        }

        [TestMethod]
        public void Parser_Required()
        {
            CommandLineParser parser;
            IEnumerable<ArgumentInstance> instances;
            TestLogger logger;

            string[] args = new string[] {  };

            // 1. Argument is required
            ArgumentDescriptor d1 = new ArgumentDescriptor("id", new string[] { "AAA" }, true /* required */, "desc1", false /* no multiples */);
            parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, false);

            logger = CheckProcessingFails(parser, args);

            logger.AssertSingleErrorExists("desc1");
            logger.AssertErrorsLogged(1);

            // 2. Argument is not required
            d1 = new ArgumentDescriptor("id", new string[] { "AAA" }, false /* not required */, "desc1", false /* no multiples */);
            parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, true);
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, args);

            AssertExpectedInstancesCount(0, instances);
        }


        [TestMethod]
        [TestCategory("Verbs")]
        public void Parser_Verbs_ExactMatchesOnly()
        {
            CommandLineParser parser;
            IEnumerable<ArgumentInstance> instances;
            TestLogger logger;

            ArgumentDescriptor verb1 = new ArgumentDescriptor("v1", new string[] { "begin" }, false /* not required */, "desc1", false /* no multiples */, true);
            parser = new CommandLineParser(new ArgumentDescriptor[] { verb1 }, true /* allow unrecognised */);

            // 1. Exact match -> matched
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, "begin");
            AssertExpectedValue("v1", "", instances);
            AssertExpectedInstancesCount(1, instances);

            // 2. Partial match -> not matched
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, "beginX");
            AssertExpectedInstancesCount(0, instances);

            // 3. Combination -> only exact matches matched
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, "beginX", "begin", "beginY");
            Assert.AreEqual(string.Empty, instances.First().Value, "Value for verb should be empty");
            AssertExpectedInstancesCount(1, instances);
            AssertExpectedValue("v1", "", instances);
        }

        [TestMethod]
        [TestCategory("Verbs")]
        public void Parser_Verbs_Multiples()
        {
            CommandLineParser parser;
            IEnumerable<ArgumentInstance> instances;
            TestLogger logger;

            string[] args = new string[] { "verb" };

            ArgumentDescriptor verb1 = new ArgumentDescriptor("v1", new string[] { "noMult" }, false /* required */, "noMult desc", false /* no multiples */, true);
            ArgumentDescriptor verb2 = new ArgumentDescriptor("v2", new string[] { "multOk" }, false /* required */, "multOk desc", true /* allow multiples */, true);

            parser = new CommandLineParser(new ArgumentDescriptor[] { verb1, verb2 }, true /* allow unrecognised */ );

            // 1. Allowed multiples
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, "multOk", "multOk");
            AssertExpectedInstancesCount(2, instances);

            // 2. Disallowed multiples
            logger = CheckProcessingFails(parser, new string[] { "noMult", "noMult" });
            logger.AssertSingleErrorExists("noMult");
            logger.AssertErrorsLogged(1);
        }

        [TestMethod]
        [TestCategory("Verbs")]
        public void Parser_Verbs_Required()
        {
            CommandLineParser parser;
            IEnumerable<ArgumentInstance> instances;
            TestLogger logger;

            string[] emptyArgs = new string[] { };
            string[] matchingPrefixArgs = new string[] { "AAAa" };

            // 1a. Argument is required but is missing -> error
            ArgumentDescriptor d1 = new ArgumentDescriptor("id", new string[] { "AAA" }, true /* required */, "desc1", false /* no multiples */, true);
            parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, false);
            logger = CheckProcessingFails(parser, emptyArgs);

            logger.AssertSingleErrorExists("desc1");
            logger.AssertErrorsLogged(1);

            // 1b. Argument is required but is only partial match -> missing -> error2
            logger = CheckProcessingFails(parser, matchingPrefixArgs);

            logger.AssertSingleErrorExists("desc1"); // missing arg
            logger.AssertSingleErrorExists("AAAa"); // unrecognized since not exact match
            logger.AssertErrorsLogged(2);


            // 2a. Argument is not required, missing -> ok
            d1 = new ArgumentDescriptor("id", new string[] { "AAA" }, false /* not required */, "desc1", false /* no multiples */, true);
            parser = new CommandLineParser(new ArgumentDescriptor[] { d1 }, true);
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, emptyArgs);

            AssertExpectedInstancesCount(0, instances);

            // 2b. Argument is not required, partial -> missing -> ok
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger, matchingPrefixArgs);

            AssertExpectedInstancesCount(0, instances);
        }

        [TestMethod]
        [TestCategory("Verbs")]
        public void Parser_OverlappingVerbsAndPrefixes()
        {
            // Tests handling of verbs and non-verbs that start with the same values
            CommandLineParser parser;
            IEnumerable<ArgumentInstance> instances;
            TestLogger logger;

            ArgumentDescriptor verb1 = new ArgumentDescriptor("v1", new string[] { "X" }, false /* not required */, "verb1 desc", false /* no multiples */, true);
            ArgumentDescriptor prefix1 = new ArgumentDescriptor("p1", new string[] { "XX" }, false /* not required */, "prefix1 desc", false /* no multiples */, false);
            ArgumentDescriptor verb2 = new ArgumentDescriptor("v2", new string[] { "XXX" }, false /* not required */, "verb2 desc", false /* no multiples */, true);
            ArgumentDescriptor prefix2 = new ArgumentDescriptor("p2", new string[] { "XXXX" }, false /* not required */, "prefix2 desc", false /* no multiples */, false);
            
            // NOTE: this test only works because the descriptors are supplied to parser ordered
            // by decreasing prefix length
            parser = new CommandLineParser(new ArgumentDescriptor[] { prefix2, verb2, prefix1, verb1 }, true /* allow unrecognised */);

            // 1. Exact match -> matched
            logger = new TestLogger();
            instances = CheckProcessingSucceeds(parser, logger,
                "X", // verb 1 - exact match
                "XXAAA", // prefix 1 - has value A,
                "XXX", // verb 2 - exact match,
                "XXXXB" // prefix 2 - has value B,
                );

            AssertExpectedValue("v1", "", instances);
            AssertExpectedValue("p1", "AAA", instances);
            AssertExpectedValue("v2", "", instances);
            AssertExpectedValue("p2", "B", instances);
        }

        #endregion

        #region Checks

        private static IEnumerable<ArgumentInstance> CheckProcessingSucceeds(CommandLineParser parser, TestLogger logger, params string[] args)
        {
            IEnumerable<ArgumentInstance> instances;
            bool success = parser.ParseArguments(args, logger, out instances);
            Assert.IsTrue(success, "Expecting parsing to succeed");
            Assert.IsNotNull(instances, "Instances should not be null if parsing succeeds");
            logger.AssertErrorsLogged(0);
            return instances;
        }

        private static TestLogger CheckProcessingFails(CommandLineParser parser, params string[] args)
        {
            TestLogger logger = new TestLogger();
            IEnumerable<ArgumentInstance> instances;
            bool success = parser.ParseArguments(args, logger, out instances);

            Assert.IsFalse(success, "Expecting parsing to fail");
            Assert.IsNotNull(instances, "Instances should not be null even if parsing fails");
            AssertExpectedInstancesCount(0, instances);

            logger.AssertErrorsLogged();

            return logger;
        }

        private static void AssertExpectedInstancesCount(int expected, IEnumerable<ArgumentInstance> actual)
        {
            Assert.AreEqual(expected, actual.Count(), "Unexpected number of arguments recognized");
        }

        private static void AssertExpectedValue(string id, string expectedValue, IEnumerable<ArgumentInstance> actual)
        {
            ArgumentInstance actualInstance;
            bool found = ArgumentInstance.TryGetArgument(id, actual, out actualInstance);
            Assert.IsTrue(found, "Expected argument was not found. Id: {0}", id);
            Assert.IsNotNull(actual);

            Assert.AreEqual(expectedValue, actualInstance.Value, "Unexpected instance value. Id: {0}", id);

            string[] actualValues = actual.Where(a => ArgumentDescriptor.IdComparer.Equals(a.Descriptor.Id, id)).Select(a => a.Value).ToArray();
            Assert.AreEqual(1, actualValues.Length, "Not expecting to find multiple values. Id: {0}", id);
        }

        private static void AssertExpectedValues(string id, IEnumerable<ArgumentInstance> actual, params string[] expectedValues)
        {
            string[] actualValues = actual.Where(a => ArgumentDescriptor.IdComparer.Equals(a.Descriptor.Id, id)).Select(a => a.Value).ToArray();

            CollectionAssert.AreEqual(expectedValues, actualValues);
        }
            #endregion
        }
    }
