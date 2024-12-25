using Dapper;
using System.Data;
using System.Data.Common;

namespace SpeedTest_CN
{
    public class DatabaseInitializer
    {
        private readonly IDbConnection _dbConnection;

        public DatabaseInitializer(IDbConnection dbConnection)
        {
            _dbConnection = dbConnection;
        }

        public void Initialize()
        {
            string tableName = "speedrecord";
            if (TableExists(tableName))
            {
                string createTableSql = @"
                                    CREATE TABLE public.speedrecord (
                                    	id varchar(36) NOT NULL,
                                    	ping varchar(200) NULL,
                                    	download numeric(11, 2) NULL,
                                    	upload numeric(11, 2) NULL,
                                    	server_id float8 NULL,
                                    	server_host varchar(500) NULL,
                                    	server_name varchar(500) NULL,
                                    	url varchar(500) NULL,
                                    	scheduled float8 DEFAULT 0 NULL,
                                    	failed float8 DEFAULT 0 NULL,
                                    	created_at timestamp DEFAULT LOCALTIMESTAMP(0) NULL,
                                    	updated_at timestamp DEFAULT LOCALTIMESTAMP(0) NULL,
                                    	CONSTRAINT speedrecord_pk PRIMARY KEY (id)
                                    );";
                _dbConnection.Execute(createTableSql);
            }
        }
        private bool TableExists(string tableName)
        {
            string checkTableSql = $@"select count(0) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '{tableName}';";
            return _dbConnection.Query<int>(checkTableSql).First() == 0 ? true : false;
        }
    }
}
