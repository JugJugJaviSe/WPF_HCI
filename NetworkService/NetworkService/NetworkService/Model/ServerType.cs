using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworkService.Model
{
    public enum TypeName
    {
        Web,
        File,
        Database
    }

    public class ServerType
    {
        public TypeName Name { get; }
        public string ImagePath { get; }

        public ServerType(TypeName name)
        {
            Name = name;
            ImagePath = GetImagePath(name);
        }

        private string GetImagePath(TypeName name)
        {
            switch (name)
            {
                case TypeName.Web:
                    return "pack://application:,,,/NetworkService;component/ServerTypeImages/WebServer.png";
                case TypeName.File:
                    return "pack://application:,,,/NetworkService;component/ServerTypeImages/FileServer.png";
                case TypeName.Database:
                    return "pack://application:,,,/NetworkService;component/ServerTypeImages/DatabaseServer.png";
                default:
                    return null;
            }
        }

    }
}
