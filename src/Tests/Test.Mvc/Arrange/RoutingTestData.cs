using System.Collections;
using System.Collections.Generic;

namespace Test.Mvc.Arrange
{

    public class RoutingTestData : IEnumerable<object[]>
    {
        public IEnumerator<object[]> GetEnumerator()
        {
            yield return new object[] { "GET", "/", "get/" };
            yield return new object[] { "POST", "/", "post/" };
            yield return new object[] { "GET", "/a", "geta/" };
            yield return new object[] { "POST", "/a", "posta/" };
            yield return new object[] { "GET", "/b/3", "getb/3" };
            yield return new object[] { "POST", "/b/3", "postb/3" };
            yield return new object[] { "GET", "/b", "getb/" };
            yield return new object[] { "POST", "/b", "postb/" };
            yield return new object[] { "GET", "/c/4", "getc/4" };
            yield return new object[] { "POST", "/c/4", "postc/4" };
            yield return new object[] { "GET", "/d/5", "getd/5/" };
            yield return new object[] { "POST", "/d/5", "postd/5/" };
            yield return new object[] { "GET", "/e/6/7", "gete/6/7" };
            yield return new object[] { "POST", "/e/6/7", "poste/6/7" };
            yield return new object[] { "GET", "/getaa", "geta/" };
            yield return new object[] { "POST", "/postaa", "posta/" };
            yield return new object[] { "GET", "/getbb/3", "getb/3" };
            yield return new object[] { "POST", "/postbb/3", "postb/3" };
            yield return new object[] { "GET", "/getbb", "getb/" };
            yield return new object[] { "POST", "/postbb", "postb/" };
            yield return new object[] { "GET", "/getcc/4", "getc/4" };
            yield return new object[] { "POST", "/postcc/4", "postc/4" };
            yield return new object[] { "GET", "/getdd/5", "getd/5/" };
            yield return new object[] { "POST", "/postdd/5", "postd/5/" };
            yield return new object[] { "GET", "/getee/6/7", "gete/6/7" };
            yield return new object[] { "POST", "/postee/6/7", "poste/6/7" };
            yield return new object[] { "GET", "/test", "get/" };
            yield return new object[] { "POST", "/test", "post/" };
            yield return new object[] { "GET", "/test/a", "geta/" };
            yield return new object[] { "POST", "/test/a", "posta/" };
            yield return new object[] { "GET", "/test/b/3", "getb/3" };
            yield return new object[] { "POST", "/test/b/3", "postb/3" };
            yield return new object[] { "GET", "/test/b", "getb/" };
            yield return new object[] { "POST", "/test/b", "postb/" };
            yield return new object[] { "GET", "/test/c/4", "getc/4" };
            yield return new object[] { "POST", "/test/c/4", "postc/4" };
            yield return new object[] { "GET", "/test/d/5", "getd/5/" };
            yield return new object[] { "POST", "/test/d/5", "postd/5/" };
            yield return new object[] { "GET", "/test/e/6/7", "gete/6/7" };
            yield return new object[] { "POST", "/test/e/6/7", "poste/6/7" };
            yield return new object[] { "GET", "/test/getaa", "geta/" };
            yield return new object[] { "POST", "/test/postaa", "posta/" };
            yield return new object[] { "GET", "/test/getbb/3", "getb/3" };
            yield return new object[] { "POST", "/test/postbb/3", "postb/3" };
            yield return new object[] { "GET", "/test/getbb", "getb/" };
            yield return new object[] { "POST", "/test/postbb", "postb/" };
            yield return new object[] { "GET", "/test/getcc/4", "getc/4" };
            yield return new object[] { "POST", "/test/postcc/4", "postc/4" };
            yield return new object[] { "GET", "/test/getdd/5", "getd/5/" };
            yield return new object[] { "POST", "/test/postdd/5", "postd/5/" };
            yield return new object[] { "GET", "/test/getee/6/7", "gete/6/7" };
            yield return new object[] { "POST", "/test/postee/6/7", "poste/6/7" };
            yield return new object[] { "GET", "/test1", "get/" };
            yield return new object[] { "POST", "/test1", "post/" };
            yield return new object[] { "GET", "/test1/a", "geta/" };
            yield return new object[] { "POST", "/test1/a", "posta/" };
            yield return new object[] { "GET", "/test1/b/3", "getb/3" };
            yield return new object[] { "POST", "/test1/b/3", "postb/3" };
            yield return new object[] { "GET", "/test1/b", "getb/" };
            yield return new object[] { "POST", "/test1/b", "postb/" };
            yield return new object[] { "GET", "/test1/c/4", "getc/4" };
            yield return new object[] { "POST", "/test1/c/4", "postc/4" };
            yield return new object[] { "GET", "/test1/d/5", "getd/5/" };
            yield return new object[] { "POST", "/test1/d/5", "postd/5/" };
            yield return new object[] { "GET", "/test1/e/6/7", "gete/6/7" };
            yield return new object[] { "POST", "/test1/e/6/7", "poste/6/7" };
            yield return new object[] { "GET", "/test1/getaa", "geta/" };
            yield return new object[] { "POST", "/test1/postaa", "posta/" };
            yield return new object[] { "GET", "/test1/getbb/3", "getb/3" };
            yield return new object[] { "POST", "/test1/postbb/3", "postb/3" };
            yield return new object[] { "GET", "/test1/getbb", "getb/" };
            yield return new object[] { "POST", "/test1/postbb", "postb/" };
            yield return new object[] { "GET", "/test1/getcc/4", "getc/4" };
            yield return new object[] { "POST", "/test1/postcc/4", "postc/4" };
            yield return new object[] { "GET", "/test1/getdd/5", "getd/5/" };
            yield return new object[] { "POST", "/test1/postdd/5", "postd/5/" };
            yield return new object[] { "GET", "/test1/getee/6/7", "gete/6/7" };
            yield return new object[] { "POST", "/test1/postee/6/7", "poste/6/7" };
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}