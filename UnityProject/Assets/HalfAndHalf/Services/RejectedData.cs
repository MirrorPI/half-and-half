using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace HalfAndHalf.Services
{
    // Preserve exact bytes before recovery/migration can overwrite a failed generation.
    sealed class RejectedData
    {
        readonly HashSet<string> pending=new HashSet<string>();
        public bool Preserved { get; private set; }
        public bool Pending=>pending.Count>0;
        public void Add(string file){if(File.Exists(file))pending.Add(file);Preserve();}
        public bool Preserve()
        {
            foreach(var file in new List<string>(pending))
            {
                try
                {
                    if(File.Exists(file))
                    {
                        byte[] bytes=File.ReadAllBytes(file);string digest;
                        using(var hash=SHA256.Create())digest=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","");
                        string destination=file+".rejected-"+digest;
                        if(!File.Exists(destination))
                            using(var stream=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                            {stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                        else if(!System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(destination),bytes))return false;
                        Preserved=true;
                    }
                    pending.Remove(file);
                }
                catch(Exception e) when(e is IOException||e is UnauthorizedAccessException){return false;}
            }
            return !Pending;
        }
    }
}
