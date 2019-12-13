using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace EbisExporter
{
    public sealed class EbisExporterProperties
    {
        /// <summary>
        /// 
        /// </summary>
        private string clienturl;

        /// <summary>
        /// 
        /// </summary>
        private string clientuser;

        /// <summary>
        /// 
        /// </summary>
        private string clientpassword;

        /// <summary>
        /// 
        /// </summary>
        private string instance;

        /// <summary>
        /// 
        /// </summary>
        private string schema;

        /// <summary>
        /// 
        /// </summary>
        private string repository;

        /// <summary>
        /// 
        /// </summary>
        private string exportpath;

        /// <summary>
        /// 
        /// </summary>
        private string connectionstring;

        /// <summary>
        /// 
        /// </summary>
        public string ClientUrl
        {
            get
            {
                return clienturl;
            }

            set
            {
                clienturl = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public string ClientUser
        {
            get
            {
                return clientuser;
            }

            set
            {
                clientuser = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public string ClientPassword
        {
            get
            {
                return clientpassword;
            }

            set
            {
                clientpassword = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public string Instance
        {
            get
            {
                return instance;
            }

            set
            {
                instance = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public string Schema
        {
            get
            {
                return schema;
            }

            set
            {
                schema = value;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public string Repository
        {
            get
            {
                return repository;
            }

            set
            {
                repository = value;
            }
        }

        public string ExportPath
        {
            get
            {
                return exportpath;
            }

            set
            {
                exportpath = value;
            }
        }

        public string ConnectionString
        {
            get
            {
                return connectionstring;
            }

            set
            {
                connectionstring = value;
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="ClientUrl"></param>
        /// <param name="Instance"></param>
        /// <param name="ClientUser"></param>
        /// <param name="ClientPassword"></param>
        /// <param name="Schema"></param>
        /// <param name="Repository"></param
        [Newtonsoft.Json.JsonConstructor]
        public EbisExporterProperties(string ClientUrl, string Instance, string ClientUser, string ClientPassword, string Schema, string Repository, string ExportPath, string ConnectionString)
        {
            this.clienturl = ClientUrl;
            this.instance = Instance;
            this.clientuser = ClientUser;
            this.clientpassword = ClientPassword;
            this.schema = Schema;
            this.repository = Repository;
            this.exportpath = ExportPath;
            this.connectionstring = ConnectionString;
        }

        /// <summary>
        /// 
        /// </summary>
        public EbisExporterProperties() : this(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty) { }

        /// <summary>
        /// prüft die einstelllungen
        /// </summary>
        public void CheckProperties()
        {
            // alles strings holen und auf vollständigkeit prüfen
            PropertyInfo[] stringProperties = this.GetType().GetProperties().Where(a => a.PropertyType == typeof(string)).ToArray();

            foreach(PropertyInfo stringProperty in stringProperties)
            {
                string name = stringProperty.Name;
                object value = stringProperty.GetValue(this);

                // auf leeren wert prüfen
                if(value == null || (value != null && value.GetType() == typeof(string) && String.IsNullOrEmpty(value.ToString())))
                    throw new ArgumentException(string.Format("Ein leerer Wert für den Paramter {0} ist ungültig!", name));
            }
        }
    }
}
