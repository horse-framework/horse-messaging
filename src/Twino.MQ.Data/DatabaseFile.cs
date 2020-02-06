using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Twino.MQ.Data
{
    public class DatabaseFile
    {
        public string Filename { get; }

        private FileStream _file;
        private FileStream _shrink;

        public DatabaseFile(string filename)
        {
            Filename = filename;
        }
        
        public Stream GetStream()
        {
            return null;
        }


        public async Task Flush()
        {
            throw new NotImplementedException();
        }

        public async Task Open()
        {
            throw new NotImplementedException();
        }
        
        public async Task Close()
        {
            throw new NotImplementedException();
        }

    }
}