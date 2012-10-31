using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace SqlServerWebApplication
{
    public partial class Home : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            var connection = new SqlConnection(@"Data Source=.\SQLEXPRESS;Initial Catalog=SqlServerWebApplication;Integrated Security=True;");
            connection.Open();

            var command = connection.CreateCommand();

            command.CommandText = "select id, name from customer";

            var reader = command.ExecuteReader(CommandBehavior.CloseConnection);

            while(reader.Read())
            {
                var id = reader.GetInt32(0);
                var name = reader.GetString(1);

                Response.Write(string.Format("{0}: {1}<br/>", id, name));
            }

            reader.Close();

        }
    }
}