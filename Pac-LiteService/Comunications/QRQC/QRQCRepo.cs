using System;
using System.Configuration;
using System.Data.SqlClient;

namespace SNPService.Comunications.QRQC
{
    internal class Repo
    {
        public Line line;                       // the line we are talking about

        public Repo(Line l)
        {
            line = l;
        }

        public string GetProductFamilyId(string ProductName)
        {
            string ProductFamilyId = "";        //initialize as empty
            string productTable = ConfigurationManager.AppSettings["camProductTable"];//this is the table that stores all product information
            string productBaseTable = ConfigurationManager.AppSettings["camProductBaseTable"]; //this is the table for the bases
            string query = "SELECT ProductFamilyId FROM " + productTable + " WHERE ProductBaseId=(SELECT ProductBaseId FROM " + productBaseTable + " WHERE ProductName='" + ProductName + "')";//select the product id where the product name is correct
            using (SqlConnection con = new SqlConnection())
            {
                con.ConnectionString = ConfigurationManager.AppSettings["DBCamstarConnectionString"] + "User Id= camstaruser; Password= c@mst@rus3r;";
                con.Open();
                try
                {
                    SqlCommand command = new SqlCommand(query, con);//submit command
                    SqlDataReader reader = command.ExecuteReader();//read the values back
                    if (reader.Read())
                    {
                        if (!reader.IsDBNull(0)) //if not null
                        {
                            ProductFamilyId = reader.GetString(0);
                        }
                    }
                }
                catch
                {
                }
            }

            return ProductFamilyId;
        } //Gets ProductFamilyId from ProductName

        public string GetProductId(string ProductName)
        {
            string id = "";
            string dbTable = ConfigurationManager.AppSettings["QRQC_ProductNameId_view"];
            string query = "SELECT ProductId FROM " + dbTable + " WHERE ProductName='" + ProductName + "'";
            using (SqlConnection con = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
            {
                con.Open();
                try
                {
                    SqlCommand command = new SqlCommand(query, con);
                    SqlDataReader reader = command.ExecuteReader();
                    if (reader.Read())
                    {
                        id = reader.GetString(0);
                    }
                }
                catch (Exception ex)
                {
                    SNPService.DiagnosticOut(ex.ToString(), 1);
                }
            }

            return id;
        }

        public int GetOutGoal(string ProductName) //gets goal of product/family/line in that order of importance
        {
            double t = 0; //we'll call this a timespan for now

            string speedTable = ConfigurationManager.AppSettings["speedTable"];

            string query = "SELECT * FROM " + speedTable + " WHERE ResourceID='" + line.Name + "' AND ProductId='" + GetProductId(ProductName) + "'";

            using (SqlConnection con = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
            {
                con.Open();
                try
                {
                    SqlCommand command = new SqlCommand(query, con);
                    SqlDataReader reader = command.ExecuteReader();

                    if (reader.HasRows)
                    {
                        reader.Read();
                        t = reader.GetDouble(3);
                    }
                    else
                    {
                        reader.Close();
                        query = "SELECT * FROM " + speedTable + " WHERE ResourceID='" + line.Name + "' AND ProductFamilyId='" + GetProductFamilyId(ProductName) + "'";
                        SqlCommand command2 = new SqlCommand(query, con);
                        SqlDataReader reader2 = command2.ExecuteReader();
                        if (reader2.HasRows)
                        {
                            reader2.Read();
                            t = reader2.GetDouble(3);
                        }
                        else
                        {
                            reader2.Close();
                            query = "SELECT * FROM " + speedTable + "WHERE ResourceID='" + line.Name + "' AND ProductFamilyId IS NULL AND ProductId IS NULL";
                            SqlCommand command3 = new SqlCommand(query, con);
                            SqlDataReader reader3 = command3.ExecuteReader();
                            if (reader3.HasRows)
                            {
                                reader3.Read();
                                t = reader3.GetDouble(3);
                                reader3.Close();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SNPService.DiagnosticOut(ex.ToString(), 1);
                }
            }

            return Convert.ToInt32(t);
        }

        public int GetOutTheo(string ProductName) //gets hour theoretical/thru of product/family/line in that order of importance
        {
            double t = 0; //we'll call this a timespan for now

            string speedTable = ConfigurationManager.AppSettings["speedTable"];

            string query = "SELECT * FROM " + speedTable + " WHERE ResourceID='" + line.Name + "' AND ProductId='" + GetProductId(ProductName) + "'";

            using (SqlConnection con = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
            {
                con.Open();
                try
                {
                    SqlCommand command = new SqlCommand(query, con);
                    SqlDataReader reader = command.ExecuteReader();

                    if (reader.HasRows)
                    {
                        reader.Read();
                        t = reader.GetDouble(4);
                    }
                    else
                    {
                        reader.Close();
                        query = "SELECT * FROM " + speedTable + " WHERE ResourceID='" + line.Name + "' AND ProductFamilyId='" + GetProductFamilyId(ProductName) + "'";
                        SqlCommand command2 = new SqlCommand(query, con);
                        SqlDataReader reader2 = command2.ExecuteReader();
                        if (reader2.HasRows)
                        {
                            reader2.Read();
                            t = reader2.GetDouble(4);
                        }
                        else
                        {
                            reader2.Close();
                            query = "SELECT * FROM " + speedTable + "WHERE ResourceID='" + line.Name + "' AND ProductFamilyId IS NULL AND ProductId IS NULL";
                            SqlCommand command3 = new SqlCommand(query, con);
                            SqlDataReader reader3 = command3.ExecuteReader();
                            if (reader3.HasRows)
                            {
                                reader3.Read();
                                t = reader3.GetDouble(4);
                                reader3.Close();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SNPService.DiagnosticOut(ex.ToString(), 1);
                }
            }

            return Convert.ToInt32(t);
        }

        public string GerResourceID(string ResourceName)
        {
            string resourceId = "";
            string sql = "SELECT ResourceId FROM [QRQC].[dbo].[CAMSTAR_Resources] WHERE ResourceName='" + ResourceName + "'";
            using (SqlConnection con = new SqlConnection(SNPService.ENGDBConnection.ConnectionString))
            {
                con.Open();
                SqlCommand command = new SqlCommand(sql, con);
                SqlDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    if (!reader.IsDBNull(0))
                    {
                        resourceId = (string)reader[0];
                    }
                }
                reader.Close();
            }
            return resourceId;
        }
    }
}