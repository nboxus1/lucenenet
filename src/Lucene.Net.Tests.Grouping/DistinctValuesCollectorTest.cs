﻿using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Queries.Function.ValueSources;
using Lucene.Net.Search.Grouping.Function;
using Lucene.Net.Search.Grouping.Terms;
using Lucene.Net.Store;
using Lucene.Net.Support;
using Lucene.Net.Util;
using Lucene.Net.Util.Mutable;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Lucene.Net.Search.Grouping
{
    public class DistinctValuesCollectorTest : AbstractGroupingTestCase
    {
        private readonly static NullComparator nullComparator = new NullComparator();

        private readonly string groupField = "author";
        private readonly string dvGroupField = "author_dv";
        private readonly string countField = "publisher";
        private readonly string dvCountField = "publisher_dv";

        internal class ComparerAnonymousHelper1 : IComparer<IGroupCount<IComparable>>
        {
            private readonly DistinctValuesCollectorTest outerInstance;

            public ComparerAnonymousHelper1(DistinctValuesCollectorTest outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            public int Compare(IGroupCount<IComparable> groupCount1, IGroupCount<IComparable> groupCount2)
            {
                if (groupCount1.GroupValue == null)
                {
                    if (groupCount2.GroupValue == null)
                    {
                        return 0;
                    }
                    return -1;
                }
                else if (groupCount2.GroupValue == null)
                {
                    return 1;
                }
                else
                {
                    return groupCount1.GroupValue.CompareTo(groupCount2.GroupValue);
                }
            }
        }

        [Test]
        public virtual void TestSimple()
        {
            Random random = Random();
            FieldInfo.DocValuesType_e[] dvTypes = new FieldInfo.DocValuesType_e[]{
                FieldInfo.DocValuesType_e.NUMERIC,
                FieldInfo.DocValuesType_e.BINARY,
                FieldInfo.DocValuesType_e.SORTED,
            };
            Directory dir = NewDirectory();
            RandomIndexWriter w = new RandomIndexWriter(
                random,
                dir,
                NewIndexWriterConfig(TEST_VERSION_CURRENT,
                    new MockAnalyzer(random)).SetMergePolicy(NewLogMergePolicy()));
            bool canUseDV = !"Lucene3x".equals(w.w.Config.Codec.Name);
            FieldInfo.DocValuesType_e? dvType = canUseDV ? dvTypes[random.nextInt(dvTypes.Length)] : (FieldInfo.DocValuesType_e?)null;

            Document doc = new Document();
            AddField(doc, groupField, "1", dvType);
            AddField(doc, countField, "1", dvType);
            doc.Add(new TextField("content", "random text", Field.Store.NO));
            doc.Add(new StringField("id", "1", Field.Store.NO));
            w.AddDocument(doc);

            // 1
            doc = new Document();
            AddField(doc, groupField, "1", dvType);
            AddField(doc, countField, "1", dvType);
            doc.Add(new TextField("content", "some more random text blob", Field.Store.NO));
            doc.Add(new StringField("id", "2", Field.Store.NO));
            w.AddDocument(doc);

            // 2
            doc = new Document();
            AddField(doc, groupField, "1", dvType);
            AddField(doc, countField, "2", dvType);
            doc.Add(new TextField("content", "some more random textual data", Field.Store.NO));
            doc.Add(new StringField("id", "3", Field.Store.NO));
            w.AddDocument(doc);
            w.Commit(); // To ensure a second segment

            // 3
            doc = new Document();
            AddField(doc, groupField, "2", dvType);
            doc.Add(new TextField("content", "some random text", Field.Store.NO));
            doc.Add(new StringField("id", "4", Field.Store.NO));
            w.AddDocument(doc);

            // 4
            doc = new Document();
            AddField(doc, groupField, "3", dvType);
            AddField(doc, countField, "1", dvType);
            doc.Add(new TextField("content", "some more random text", Field.Store.NO));
            doc.Add(new StringField("id", "5", Field.Store.NO));
            w.AddDocument(doc);

            // 5
            doc = new Document();
            AddField(doc, groupField, "3", dvType);
            AddField(doc, countField, "1", dvType);
            doc.Add(new TextField("content", "random blob", Field.Store.NO));
            doc.Add(new StringField("id", "6", Field.Store.NO));
            w.AddDocument(doc);

            // 6 -- no author field
            doc = new Document();
            doc.Add(new TextField("content", "random word stuck in alot of other text", Field.Store.YES));
            AddField(doc, countField, "1", dvType);
            doc.Add(new StringField("id", "6", Field.Store.NO));
            w.AddDocument(doc);

            IndexSearcher indexSearcher = NewSearcher(w.Reader);
            w.Dispose();

            var cmp = new ComparerAnonymousHelper1(this);

            // === Search for content:random
            IAbstractFirstPassGroupingCollector<IComparable> firstCollector = CreateRandomFirstPassCollector(dvType, new Sort(), groupField, 10);
            // LUCENENET TODO: Create an ICollector interface that we can inherit our Collector interfaces from
            // so this cast is not necessary. Consider eliminating the Collector abstract class.
            indexSearcher.Search(new TermQuery(new Term("content", "random")), firstCollector as Collector);
            IAbstractDistinctValuesCollector<IGroupCount<IComparable>> distinctValuesCollector
                = CreateDistinctCountCollector(firstCollector, groupField, countField, dvType.GetValueOrDefault());
            // LUCENENET TODO: Create an ICollector interface that we can inherit our Collector interfaces from
            // so this cast is not necessary. Consider eliminating the Collector abstract class.
            indexSearcher.Search(new TermQuery(new Term("content", "random")), distinctValuesCollector as Collector);

            //var gcs = distinctValuesCollector.Groups as List<IGroupCount<IComparable>>;
            // LUCENENET TODO: Try to work out how to do this without an O(n) operation
            var gcs = new List<IGroupCount<IComparable>>(distinctValuesCollector.Groups);
            gcs.Sort(cmp);
            assertEquals(4, gcs.Count);

            CompareNull(gcs[0].GroupValue);
            List<IComparable> countValues = new List<IComparable>(gcs[0].UniqueValues);
            assertEquals(1, countValues.size());
            Compare("1", countValues[0]);

            Compare("1", gcs[1].GroupValue);
            countValues = new List<IComparable>(gcs[1].UniqueValues);
            countValues.Sort(nullComparator);
            assertEquals(2, countValues.size());
            Compare("1", countValues[0]);
            Compare("2", countValues[1]);

            Compare("2", gcs[2].GroupValue);
            countValues = new List<IComparable>(gcs[2].UniqueValues);
            assertEquals(1, countValues.size());
            CompareNull(countValues[0]);

            Compare("3", gcs[3].GroupValue);
            countValues = new List<IComparable>(gcs[3].UniqueValues);
            assertEquals(1, countValues.size());
            Compare("1", countValues[0]);

            // === Search for content:some
            firstCollector = CreateRandomFirstPassCollector(dvType, new Sort(), groupField, 10);
            // LUCENENET TODO: Create an ICollector interface that we can inherit our Collector interfaces from
            // so this cast is not necessary. Consider eliminating the Collector abstract class.
            indexSearcher.Search(new TermQuery(new Term("content", "some")), firstCollector as Collector);
            distinctValuesCollector = CreateDistinctCountCollector(firstCollector, groupField, countField, dvType);
            // LUCENENET TODO: Create an ICollector interface that we can inherit our Collector interfaces from
            // so this cast is not necessary. Consider eliminating the Collector abstract class.
            indexSearcher.Search(new TermQuery(new Term("content", "some")), distinctValuesCollector as Collector);

            // LUCENENET TODO: Try to work out how to do this without an O(n) operation
            //gcs = distinctValuesCollector.Groups as List<IGroupCount<IComparable>>;
            gcs = new List<IGroupCount<IComparable>>(distinctValuesCollector.Groups);
            gcs.Sort(cmp);
            assertEquals(3, gcs.Count);

            Compare("1", gcs[0].GroupValue);
            countValues = new List<IComparable>(gcs[0].UniqueValues);
            assertEquals(2, countValues.size());
            countValues.Sort(nullComparator);
            Compare("1", countValues[0]);
            Compare("2", countValues[1]);

            Compare("2", gcs[1].GroupValue);
            countValues = new List<IComparable>(gcs[1].UniqueValues);
            assertEquals(1, countValues.size());
            CompareNull(countValues[0]);

            Compare("3", gcs[2].GroupValue);
            countValues = new List<IComparable>(gcs[2].UniqueValues);
            assertEquals(1, countValues.size());
            Compare("1", countValues[0]);

            // === Search for content:blob
            firstCollector = CreateRandomFirstPassCollector(dvType, new Sort(), groupField, 10);
            // LUCENENET TODO: Create an ICollector interface that we can inherit our Collector interfaces from
            // so this cast is not necessary. Consider eliminating the Collector abstract class.
            indexSearcher.Search(new TermQuery(new Term("content", "blob")), firstCollector as Collector);
            distinctValuesCollector = CreateDistinctCountCollector(firstCollector, groupField, countField, dvType);
            // LUCENENET TODO: Create an ICollector interface that we can inherit our Collector interfaces from
            // so this cast is not necessary. Consider eliminating the Collector abstract class.
            indexSearcher.Search(new TermQuery(new Term("content", "blob")), distinctValuesCollector as Collector);

            // LUCENENET TODO: Try to work out how to do this without an O(n) operation
            //gcs = distinctValuesCollector.Groups as List<IGroupCount<IComparable>>;
            gcs = new List<IGroupCount<IComparable>>(distinctValuesCollector.Groups);
            gcs.Sort(cmp);
            assertEquals(2, gcs.Count);

            Compare("1", gcs[0].GroupValue);
            countValues = new List<IComparable>(gcs[0].UniqueValues);
            // B/c the only one document matched with blob inside the author 1 group
            assertEquals(1, countValues.Count);
            Compare("1", countValues[0]);

            Compare("3", gcs[1].GroupValue);
            countValues = new List<IComparable>(gcs[1].UniqueValues);
            assertEquals(1, countValues.Count);
            Compare("1", countValues[0]);

            indexSearcher.IndexReader.Dispose();
            dir.Dispose();
        }

        [Test]
        public virtual void TestRandom()
        {
            Random random = Random();
            int numberOfRuns = TestUtil.NextInt(random, 3, 6);
            for (int indexIter = 0; indexIter < numberOfRuns; indexIter++)
            {
                IndexContext context = CreateIndexContext();
                for (int searchIter = 0; searchIter < 100; searchIter++)
                {
                    IndexSearcher searcher = NewSearcher(context.indexReader);
                    bool useDv = context.dvType != null && random.nextBoolean();
                    FieldInfo.DocValuesType_e? dvType = useDv ? context.dvType : null;
                    string term = context.contentStrings[random.nextInt(context.contentStrings.Length)];
                    Sort groupSort = new Sort(new SortField("id", SortField.Type_e.STRING));
                    int topN = 1 + random.nextInt(10);

                    List<IGroupCount<IComparable>> expectedResult = CreateExpectedResult(context, term, groupSort, topN);

                    IAbstractFirstPassGroupingCollector<IComparable> firstCollector = CreateRandomFirstPassCollector(dvType, groupSort, groupField, topN);
                    // LUCENENET TODO: Create an ICollector interface that we can inherit our Collector interfaces from
                    // so this cast is not necessary. Consider eliminating the Collector abstract class.
                    searcher.Search(new TermQuery(new Term("content", term)), firstCollector as Collector);
                    IAbstractDistinctValuesCollector<IGroupCount<IComparable>> distinctValuesCollector
                        = CreateDistinctCountCollector(firstCollector, groupField, countField, dvType);
                    // LUCENENET TODO: Create an ICollector interface that we can inherit our Collector interfaces from
                    // so this cast is not necessary. Consider eliminating the Collector abstract class.
                    searcher.Search(new TermQuery(new Term("content", term)), distinctValuesCollector as Collector);

                    // LUCENENET TODO: Try to work out how to do this without an O(n) operation
                    List<IGroupCount<IComparable>> actualResult = new List<IGroupCount<IComparable>>(distinctValuesCollector.Groups);

                    if (VERBOSE)
                    {
                        Console.WriteLine("Index iter=" + indexIter);
                        Console.WriteLine("Search iter=" + searchIter);
                        Console.WriteLine("1st pass collector class name=" + firstCollector.GetType().Name);
                        Console.WriteLine("2nd pass collector class name=" + distinctValuesCollector.GetType().Name);
                        Console.WriteLine("Search term=" + term);
                        Console.WriteLine("DVType=" + dvType);
                        Console.WriteLine("1st pass groups=" + firstCollector.GetTopGroups(0, false).toString());
                        Console.WriteLine("Expected:");
                        PrintGroups(expectedResult);
                        Console.WriteLine("Actual:");
                        PrintGroups(actualResult);
                        Console.Out.Flush();
                    }

                    assertEquals(expectedResult.Count, actualResult.Count);
                    for (int i = 0; i < expectedResult.size(); i++)
                    {
                        IGroupCount<IComparable> expected = expectedResult[i];
                        IGroupCount<IComparable> actual = actualResult[i];
                        AssertValues(expected.GroupValue, actual.GroupValue);
                        assertEquals(expected.UniqueValues.Count(), actual.UniqueValues.Count());
                        List<IComparable> expectedUniqueValues = new List<IComparable>(expected.UniqueValues);
                        expectedUniqueValues.Sort(nullComparator);
                        List<IComparable> actualUniqueValues = new List<IComparable>(actual.UniqueValues);
                        actualUniqueValues.Sort(nullComparator);
                        for (int j = 0; j < expectedUniqueValues.size(); j++)
                        {
                            AssertValues(expectedUniqueValues[j], actualUniqueValues[j]);
                        }
                    }
                }
                context.indexReader.Dispose();
                context.directory.Dispose();
            }
        }

        private void PrintGroups(List<IGroupCount<IComparable>> results)
        {
            for (int i = 0; i < results.size(); i++)
            {
                var group = results[i];
                object gv = group.GroupValue;
                if (gv is BytesRef)
                {
                    Console.WriteLine(i + ": groupValue=" + ((BytesRef)gv).Utf8ToString());
                }
                else
                {
                    Console.WriteLine(i + ": groupValue=" + gv);
                }
                foreach (object o in group.UniqueValues)
                {
                    if (o is BytesRef)
                    {
                        Console.WriteLine("  " + ((BytesRef)o).Utf8ToString());
                    }
                    else
                    {
                        Console.WriteLine("  " + o);
                    }
                }
            }
        }

        private void AssertValues(object expected, object actual)
        {
            if (expected == null)
            {
                CompareNull(actual);
            }
            else
            {
                Compare(((BytesRef)expected).Utf8ToString(), actual);
            }
        }

        private void Compare(string expected, object groupValue)
        {
            if (typeof(BytesRef).IsAssignableFrom(groupValue.GetType()))
            {
                assertEquals(expected, ((BytesRef)groupValue).Utf8ToString());
            }
            else if (typeof(double).IsAssignableFrom(groupValue.GetType()))
            {
                assertEquals(double.Parse(expected, CultureInfo.InvariantCulture), groupValue);
            }
            else if (typeof(long).IsAssignableFrom(groupValue.GetType()))
            {
                assertEquals(long.Parse(expected, CultureInfo.InvariantCulture), groupValue);
            }
            else if (typeof(MutableValue).IsAssignableFrom(groupValue.GetType()))
            {
                MutableValueStr mutableValue = new MutableValueStr();
                mutableValue.Value = new BytesRef(expected);
                assertEquals(mutableValue, groupValue);
            }
            else
            {
                fail();
            }
        }

        private void CompareNull(object groupValue)
        {
            if (groupValue == null)
            {
                return; // term based impl...
            }
            // DV based impls..
            if (typeof(BytesRef).IsAssignableFrom(groupValue.GetType()))
            {
                assertEquals("", ((BytesRef)groupValue).Utf8ToString());
            }
            else if (typeof(double).IsAssignableFrom(groupValue.GetType()))
            {
                assertEquals(0.0d, groupValue);
            }
            else if (typeof(long).IsAssignableFrom(groupValue.GetType()))
            {
                assertEquals(0L, groupValue);
                // Function based impl
            }
            else if (typeof(MutableValue).IsAssignableFrom(groupValue.GetType()))
            {
                assertFalse(((MutableValue)groupValue).Exists);
            }
            else
            {
                fail();
            }
        }

        private void AddField(Document doc, string field, string value, FieldInfo.DocValuesType_e? type)
        {
            doc.Add(new StringField(field, value, Field.Store.YES));
            if (type == null)
            {
                return;
            }
            string dvField = field + "_dv";

            Field valuesField = null;
            switch (type)
            {
                case FieldInfo.DocValuesType_e.NUMERIC:
                    valuesField = new NumericDocValuesField(dvField, int.Parse(value, CultureInfo.InvariantCulture));
                    break;
                case FieldInfo.DocValuesType_e.BINARY:
                    valuesField = new BinaryDocValuesField(dvField, new BytesRef(value));
                    break;
                case FieldInfo.DocValuesType_e.SORTED:
                    valuesField = new SortedDocValuesField(dvField, new BytesRef(value));
                    break;
            }
            doc.Add(valuesField);
        }

        private IAbstractDistinctValuesCollector<IGroupCount<T>> CreateDistinctCountCollector<T>(IAbstractFirstPassGroupingCollector<T> firstPassGroupingCollector,
                                                                            string groupField,
                                                                            string countField,
                                                                            FieldInfo.DocValuesType_e? dvType)
        {
            Random random = Random();
            IEnumerable<ISearchGroup<T>> searchGroups = firstPassGroupingCollector.GetTopGroups(0, false);
            if (typeof(FunctionFirstPassGroupingCollector).IsAssignableFrom(firstPassGroupingCollector.GetType()))
            {
                return (IAbstractDistinctValuesCollector<IGroupCount<T>>)new FunctionDistinctValuesCollector(new Hashtable(), new BytesRefFieldSource(groupField), new BytesRefFieldSource(countField), searchGroups as IEnumerable<ISearchGroup<MutableValue>>);
            }
            else
            {
                return (IAbstractDistinctValuesCollector<IGroupCount<T>>)new TermDistinctValuesCollector(groupField, countField, searchGroups as IEnumerable<ISearchGroup<BytesRef>>);
            }
        }

        private IAbstractFirstPassGroupingCollector<IComparable> CreateRandomFirstPassCollector(FieldInfo.DocValuesType_e? dvType, Sort groupSort, string groupField, int topNGroups)
        {
            Random random = Random();
            if (dvType != null)
            {
                if (random.nextBoolean())
                {
                    return new FunctionFirstPassGroupingCollector(new BytesRefFieldSource(groupField), new Hashtable(), groupSort, topNGroups)
                        as IAbstractFirstPassGroupingCollector<IComparable>;
                }
                else
                {
                    return new TermFirstPassGroupingCollector(groupField, groupSort, topNGroups)
                        as IAbstractFirstPassGroupingCollector<IComparable>;
                }
            }
            else
            {
                if (random.nextBoolean())
                {
                    return new FunctionFirstPassGroupingCollector(new BytesRefFieldSource(groupField), new Hashtable(), groupSort, topNGroups)
                        as IAbstractFirstPassGroupingCollector<IComparable>;
                }
                else
                {
                    return new TermFirstPassGroupingCollector(groupField, groupSort, topNGroups)
                        as IAbstractFirstPassGroupingCollector<IComparable>;
                }
            }
        }

        internal class GroupCount : AbstractGroupCount<BytesRef>
        {
            internal GroupCount(BytesRef groupValue, IEnumerable<BytesRef> uniqueValues)
                : base(groupValue)
            {
                ((ISet<BytesRef>)this.UniqueValues).UnionWith(uniqueValues);
            }
        }

        private List<IGroupCount<IComparable>> CreateExpectedResult(IndexContext context, string term, Sort groupSort, int topN)
        {
            List<IGroupCount<IComparable>> result = new List<IGroupCount<IComparable>>();
            IDictionary<string, ISet<string>> groupCounts = context.searchTermToGroupCounts[term];
            int i = 0;
            foreach (string group in groupCounts.Keys)
            {
                if (topN <= i++)
                {
                    break;
                }
                ISet<BytesRef> uniqueValues = new HashSet<BytesRef>();
                foreach (string val in groupCounts[group])
                {
                    uniqueValues.Add(val != null ? new BytesRef(val) : null);
                }
                var gc = new GroupCount(group != null ? new BytesRef(group) : (BytesRef)null, uniqueValues);
                result.Add(gc);
            }
            return result;
        }

        private IndexContext CreateIndexContext()
        {
            Random random = Random();
                FieldInfo.DocValuesType_e[] dvTypes = new FieldInfo.DocValuesType_e[]{
                FieldInfo.DocValuesType_e.BINARY,
                FieldInfo.DocValuesType_e.SORTED
            };

            Directory dir = NewDirectory();
            RandomIndexWriter w = new RandomIndexWriter(
                random,
                dir,
                NewIndexWriterConfig(TEST_VERSION_CURRENT,
                new MockAnalyzer(random)).SetMergePolicy(NewLogMergePolicy())
              );

            bool canUseDV = !"Lucene3x".equals(w.w.Config.Codec.Name);
            FieldInfo.DocValuesType_e? dvType = canUseDV ? dvTypes[random.nextInt(dvTypes.Length)] : (FieldInfo.DocValuesType_e?)null;

            int numDocs = 86 + random.nextInt(1087) * RANDOM_MULTIPLIER;
            string[] groupValues = new string[numDocs / 5];
            string[] countValues = new string[numDocs / 10];
            for (int i = 0; i < groupValues.Length; i++)
            {
                groupValues[i] = GenerateRandomNonEmptyString();
            }
            for (int i = 0; i < countValues.Length; i++)
            {
                countValues[i] = GenerateRandomNonEmptyString();
            }

            List<string> contentStrings = new List<string>();
            IDictionary<string, IDictionary<string, ISet<string>>> searchTermToGroupCounts = new HashMap<string, IDictionary<string, ISet<string>>>();
            for (int i = 1; i <= numDocs; i++)
            {
                string groupValue = random.nextInt(23) == 14 ? null : groupValues[random.nextInt(groupValues.Length)];
                string countValue = random.nextInt(21) == 13 ? null : countValues[random.nextInt(countValues.Length)];
                string content = "random" + random.nextInt(numDocs / 20);
                IDictionary<string, ISet<string>> groupToCounts;
                if (!searchTermToGroupCounts.TryGetValue(content, out groupToCounts))
                {
                    // Groups sort always DOCID asc...
                    searchTermToGroupCounts.Add(content, groupToCounts = new LinkedHashMap<string, ISet<string>>());
                    contentStrings.Add(content);
                }

                ISet<string> countsVals;
                if (!groupToCounts.TryGetValue(groupValue, out countsVals))
                {
                    groupToCounts.Add(groupValue, countsVals = new HashSet<string>());
                }
                countsVals.Add(countValue);

                Document doc = new Document();
                doc.Add(new StringField("id", string.Format(CultureInfo.InvariantCulture, "{0:D9}", i), Field.Store.YES));
                if (groupValue != null)
                {
                    AddField(doc, groupField, groupValue, dvType);
                }
                if (countValue != null)
                {
                    AddField(doc, countField, countValue, dvType);
                }
                doc.Add(new TextField("content", content, Field.Store.YES));
                w.AddDocument(doc);
            }

            DirectoryReader reader = w.Reader;
            if (VERBOSE)
            {
                for (int docID = 0; docID < reader.MaxDoc; docID++)
                {
                    Document doc = reader.Document(docID);
                    Console.WriteLine("docID=" + docID + " id=" + doc.Get("id") + " content=" + doc.Get("content") + " author=" + doc.Get("author") + " publisher=" + doc.Get("publisher"));
                }
            }

            w.Dispose();
            return new IndexContext(dir, reader, dvType, searchTermToGroupCounts, contentStrings.ToArray(/*new String[contentStrings.size()]*/));
        }

        internal class IndexContext
        {

            internal readonly Directory directory;
            internal readonly DirectoryReader indexReader;
            internal readonly FieldInfo.DocValuesType_e? dvType;
            internal readonly IDictionary<string, IDictionary<string, ISet<string>>> searchTermToGroupCounts;
            internal readonly string[] contentStrings;

            internal IndexContext(Directory directory, DirectoryReader indexReader, FieldInfo.DocValuesType_e? dvType,
                         IDictionary<string, IDictionary<string, ISet<string>>> searchTermToGroupCounts, string[] contentStrings)
            {
                this.directory = directory;
                this.indexReader = indexReader;
                this.dvType = dvType;
                this.searchTermToGroupCounts = searchTermToGroupCounts;
                this.contentStrings = contentStrings;
            }
        }

        internal class NullComparator : IComparer<IComparable>
        {

            public int Compare(IComparable a, IComparable b)
            {
                if (a == b)
                {
                    return 0;
                }
                else if (a == null)
                {
                    return -1;
                }
                else if (b == null)
                {
                    return 1;
                }
                else
                {
                    return a.CompareTo(b);
                }
            }

        }
    }
}