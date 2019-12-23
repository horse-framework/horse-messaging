using Test.SocketModels.Models;
using Twino.SerializableModel.Serialization;
using Xunit;

namespace Test.SocketModels
{
    public class DefaultModelRW
    {
        private readonly IModelWriter _writer;
        private readonly IModelReader _reader;
        
        public DefaultModelRW()
        {
            _writer = new TwinoModelWriter();
            _reader = new TwinoModelReader();
        }

        [Fact]
        public void WriteDefaultModel()
        {
            DefaultModel model = new DefaultModel();
            model.Type = 123;
            model.Number = 500;
            model.Name = "Default";

            string serialized = _writer.Serialize(model);
            Assert.Equal("[123,{\"type\":123,\"name\":\"Default\",\"number\":500}]", serialized);
        }

        [Fact]
        public void ReadDefaultModel()
        {
            string serialized = "[123,{\"type\":123,\"name\":\"Default\",\"number\":500}]";
            
            DefaultModel model = (DefaultModel) _reader.Read(typeof(DefaultModel), serialized);
            DefaultModel gmodel = _reader.Read<DefaultModel>(serialized);

            Assert.Equal(123, model.Type);
            Assert.Equal(123, gmodel.Type);
            
            Assert.Equal(500, model.Number);
            Assert.Equal(500, gmodel.Number);
            
            Assert.Equal("Default", model.Name);
            Assert.Equal("Default", gmodel.Name);
        }

        [Fact]
        public void WriteCriticalModel()
        {
            CriticalModel model = new CriticalModel();
            model.Type = 123;
            model.Number = 500;
            model.Name = "Critical";

            string serialized = _writer.Serialize(model);
            Assert.Equal("[123,{\"type\":123,\"name\":\"Critical\",\"number\":500}]", serialized);
        }

        [Fact]
        public void ReadCriticalModel()
        {
            string serialized = "[123,{\"type\":123,\"name\":\"Critical\",\"number\":500}]";
            
            CriticalModel model = (CriticalModel) _reader.Read(typeof(CriticalModel), serialized);
            CriticalModel gmodel = _reader.Read<CriticalModel>(serialized);

            Assert.Equal(123, model.Type);
            Assert.Equal(123, gmodel.Type);
            
            Assert.Equal(500, model.Number);
            Assert.Equal(500, gmodel.Number);
            
            Assert.Equal("Critical", model.Name);
            Assert.Equal("Critical", gmodel.Name);
        }
        
        [Fact]
        public void WriteReadDefaultModel()
        {
            DefaultModel smodel = new DefaultModel();
            smodel.Type = 123;
            smodel.Number = 500;
            smodel.Name = "Default";

            string serialized = _writer.Serialize(smodel);
            
            DefaultModel model = (DefaultModel) _reader.Read(typeof(DefaultModel), serialized);
            DefaultModel gmodel = _reader.Read<DefaultModel>(serialized);

            Assert.Equal(123, model.Type);
            Assert.Equal(123, gmodel.Type);
            
            Assert.Equal(500, model.Number);
            Assert.Equal(500, gmodel.Number);
            
            Assert.Equal("Default", model.Name);
            Assert.Equal("Default", gmodel.Name);
        }

        [Fact]
        public void WriteReadCriticalModel()
        {
            CriticalModel smodel = new CriticalModel();
            smodel.Type = 123;
            smodel.Number = 500;
            smodel.Name = "Critical";
            
            string serialized = _writer.Serialize(smodel);
            
            CriticalModel model = (CriticalModel) _reader.Read(typeof(CriticalModel), serialized);
            CriticalModel gmodel = _reader.Read<CriticalModel>(serialized);

            Assert.Equal(123, model.Type);
            Assert.Equal(123, gmodel.Type);
            
            Assert.Equal(500, model.Number);
            Assert.Equal(500, gmodel.Number);
            
            Assert.Equal("Critical", model.Name);
            Assert.Equal("Critical", gmodel.Name);
        }

    }
}