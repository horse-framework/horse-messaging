using Twino.Ioc;
using Xunit;

namespace Test.Ioc
{
    public class TransientTest
    {

        [Fact]
        public void Single()
        {
            ServiceContainer container = new ServiceContainer();
        }
        
        [Fact]
        public void Nested()
        {
        }
        
        [Fact]
        public void NestedDoubleParameter()
        {
        }
        
        [Fact]
        public void MultipleNestedDoubleParameter()
        {
        }
    }
}