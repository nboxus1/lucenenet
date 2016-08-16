﻿using Lucene.Net.Analysis.Core;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.Util;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Lucene.Net.Analysis.Miscellaneous
{
    /*
     * Licensed to the Apache Software Foundation (ASF) under one or more
     * contributor license agreements.  See the NOTICE file distributed with
     * this work for additional information regarding copyright ownership.
     * The ASF licenses this file to You under the Apache License, Version 2.0
     * (the "License"); you may not use this file except in compliance with
     * the License.  You may obtain a copy of the License at
     *
     *     http://www.apache.org/licenses/LICENSE-2.0
     *
     * Unless required by applicable law or agreed to in writing, software
     * distributed under the License is distributed on an "AS IS" BASIS,
     * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
     * See the License for the specific language governing permissions and
     * limitations under the License.
     */

    public class TestTrimFilter : BaseTokenStreamTestCase
    {

        [Test]
        public virtual void TestTrim()
        {
            char[] a = " a ".ToCharArray();
            char[] b = "b   ".ToCharArray();
            char[] ccc = "cCc".ToCharArray();
            char[] whitespace = "   ".ToCharArray();
            char[] empty = "".ToCharArray();

            TokenStream ts = new IterTokenStream(new Token(a, 0, a.Length, 1, 5), new Token(b, 0, b.Length, 6, 10), new Token(ccc, 0, ccc.Length, 11, 15), new Token(whitespace, 0, whitespace.Length, 16, 20), new Token(empty, 0, empty.Length, 21, 21));
            ts = new TrimFilter(TEST_VERSION_CURRENT, ts, false);

            AssertTokenStreamContents(ts, new string[] { "a", "b", "cCc", "", "" });

            a = " a".ToCharArray();
            b = "b ".ToCharArray();
            ccc = " c ".ToCharArray();
            whitespace = "   ".ToCharArray();
            ts = new IterTokenStream(new Token(a, 0, a.Length, 0, 2), new Token(b, 0, b.Length, 0, 2), new Token(ccc, 0, ccc.Length, 0, 3), new Token(whitespace, 0, whitespace.Length, 0, 3));
            ts = new TrimFilter(LuceneVersion.LUCENE_43, ts, true);

            AssertTokenStreamContents(ts, new string[] { "a", "b", "c", "" }, new int[] { 1, 0, 1, 3 }, new int[] { 2, 1, 2, 3 }, null, new int[] { 1, 1, 1, 1 }, null, null, false);
        }

        /// @deprecated (3.0) does not support custom attributes 
        [Obsolete("(3.0) does not support custom attributes")]
        private class IterTokenStream : TokenStream
        {
            internal readonly Token[] tokens;
            internal int index = 0;
            internal ICharTermAttribute termAtt;
            internal IOffsetAttribute offsetAtt;
            internal IPositionIncrementAttribute posIncAtt;
            internal IFlagsAttribute flagsAtt;
            internal ITypeAttribute typeAtt;
            internal IPayloadAttribute payloadAtt;

            public IterTokenStream(params Token[] tokens)
                    : base()
            {
                this.tokens = tokens;
                this.termAtt = AddAttribute<ICharTermAttribute>();
                this.offsetAtt = AddAttribute<IOffsetAttribute>();
                this.posIncAtt = AddAttribute<IPositionIncrementAttribute>();
                this.flagsAtt = AddAttribute<IFlagsAttribute>();
                this.typeAtt = AddAttribute<ITypeAttribute>();
                this.payloadAtt = AddAttribute<IPayloadAttribute>();
            }

            public IterTokenStream(ICollection<Token> tokens)
                    : this(tokens.ToArray())
            {
            }

            public override sealed bool IncrementToken()
            {
                if (index >= tokens.Length)
                {
                    return false;
                }
                else
                {
                    ClearAttributes();
                    Token token = tokens[index++];
                    termAtt.SetEmpty().Append(token);
                    offsetAtt.SetOffset(token.StartOffset(), token.EndOffset());
                    posIncAtt.PositionIncrement = token.PositionIncrement;
                    flagsAtt.Flags = token.Flags;
                    typeAtt.Type = token.Type;
                    payloadAtt.Payload = token.Payload;
                    return true;
                }
            }
        }

        /// <summary>
        /// blast some random strings through the analyzer </summary>
        [Test]
        public virtual void TestRandomStrings()
        {
            Analyzer a = new AnalyzerAnonymousInnerClassHelper(this);
            CheckRandomData(Random(), a, 1000 * RANDOM_MULTIPLIER);

            Analyzer b = new AnalyzerAnonymousInnerClassHelper2(this);
            CheckRandomData(Random(), b, 1000 * RANDOM_MULTIPLIER);
        }

        private class AnalyzerAnonymousInnerClassHelper : Analyzer
        {
            private readonly TestTrimFilter outerInstance;

            public AnalyzerAnonymousInnerClassHelper(TestTrimFilter outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            public override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
            {
                Tokenizer tokenizer = new MockTokenizer(reader, MockTokenizer.KEYWORD, false);
                return new TokenStreamComponents(tokenizer, new TrimFilter(LuceneVersion.LUCENE_43, tokenizer, true));
            }
        }

        private class AnalyzerAnonymousInnerClassHelper2 : Analyzer
        {
            private readonly TestTrimFilter outerInstance;

            public AnalyzerAnonymousInnerClassHelper2(TestTrimFilter outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            public override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
            {
                Tokenizer tokenizer = new MockTokenizer(reader, MockTokenizer.KEYWORD, false);
                return new TokenStreamComponents(tokenizer, new TrimFilter(TEST_VERSION_CURRENT, tokenizer, false));
            }
        }

        [Test]
        public virtual void TestEmptyTerm()
        {
            Analyzer a = new AnalyzerAnonymousInnerClassHelper3(this);
            CheckOneTerm(a, "", "");
        }

        private class AnalyzerAnonymousInnerClassHelper3 : Analyzer
        {
            private readonly TestTrimFilter outerInstance;

            public AnalyzerAnonymousInnerClassHelper3(TestTrimFilter outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            public override TokenStreamComponents CreateComponents(string fieldName, TextReader reader)
            {
                Tokenizer tokenizer = new KeywordTokenizer(reader);
                bool updateOffsets = Random().nextBoolean();
                LuceneVersion version = updateOffsets ? LuceneVersion.LUCENE_43 : TEST_VERSION_CURRENT;
                return new TokenStreamComponents(tokenizer, new TrimFilter(version, tokenizer, updateOffsets));
            }
        }
    }
}