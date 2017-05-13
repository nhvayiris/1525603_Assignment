﻿using PayPal.Api;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web;

namespace assignment_draft.Members
{
    public partial class Checkout : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            string ss = HttpContext.Current.User.Identity.Name; //taking the current logged in user as ss

            SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["qiwebcon"].ConnectionString);
            con.Open();

            SqlCommand cmd = new SqlCommand("SELECT ClientId , Sum(tbl_products.Price*tb_Cart.Quantity) As subTotal, SUM(tb_Cart.Quantity) As totalQty FROM [tb_Cart] INNER JOIN tbl_products ON tbl_products.ProductId = tb_Cart.ProductId WHERE ClientId = '" + ss + "'" + " group by tb_Cart.ClientId", con);
            SqlDataReader sdr;
            sdr = cmd.ExecuteReader();
            while (sdr.Read())
            {
                decimal subb = decimal.Parse(sdr["subTotal"].ToString());
                lbtotal.Text = subb.ToString("0.00") ;
                lblTotalQty.Text = sdr["totalQty"].ToString();
                lbGrandTotal.Text = subb.ToString("0.00");
            } //read data from database and insert them into labels

            sdr.Close();
            con.Close();

            con.Open();

            //create a datasource for the repeaters
            SqlCommand cmd1 = new SqlCommand("SELECT ClientId ,tb_Cart.[ProductId] ,[Quantity] ,[Extension], tbl_products.ProductName, tbl_products.Price, tbl_products.ImageId, tbl_products.CategoryId FROM [tb_Cart] INNER JOIN tbl_products  ON tbl_products.ProductId = tb_Cart.ProductId  WHERE ClientId ='" + HttpContext.Current.User.Identity.Name + "'", con);
            //cmd1.CommandType = CommandType.StoredProcedure;
            SqlDataAdapter sda = new SqlDataAdapter(cmd1);
            DataTable dt = new DataTable();
            sda.Fill(dt);
            rptrCart.DataSource = dt;
            rptrCart.DataBind();
            cmd1.Dispose();


        }

        protected void btnPaypalCheckout_Click(object sender, EventArgs e) //checkout method here
        {
            try
            {
                using (SqlConnection sc = new SqlConnection(ConfigurationManager.ConnectionStrings["qiwebcon"].ConnectionString))
                {

                    using (SqlCommand comm = new SqlCommand("SELECT tbl_products.ProductId As id ,tb_Cart.Quantity As Qty, tbl_products.Price As cost, tbl_products.ProductName As Names FROM [tb_Cart] INNER JOIN tbl_products ON tbl_products.ProductId = tb_Cart.ProductId ", sc))

                    {
                        sc.Open();

                        //declaring variables
                        decimal subb = 0m;

                        var config = ConfigManager.Instance.GetProperties();
                        var accessToken = new OAuthTokenCredential(config).GetAccessToken();
                        var apiContext = new APIContext(accessToken);

                        var planner = new Item();

                        //SqlDataReader starts here
                        using (SqlDataReader sdr = comm.ExecuteReader())
                        {
                            List<Item> pOrders = new List<Item>();
                            while (sdr.Read()) { //get value from db and store into objects

                                string names = sdr["Names"].ToString();
                                string prices = sdr["cost"].ToString();
                                string skuV = sdr["id"].ToString();
                                string qty = sdr["Qty"].ToString();

                                planner.name = names;
                                planner.currency = "GBP";
                                planner.price = prices;
                                planner.sku = skuV;
                                planner.quantity = qty;
                                subb = Convert.ToDecimal(prices) * Convert.ToDecimal(qty);

                                pOrders.Add(planner);

                            } //while loop ends here


                            var transactionDetails = new Details(); //gets details for refund transactions
                            transactionDetails.tax = "0";
                            transactionDetails.shipping = "0";
                            transactionDetails.subtotal = subb.ToString("0.00");

                            var transactionAmount = new Amount();  //shows info for payments
                            transactionAmount.currency = "GBP";
                            transactionAmount.total = subb.ToString("0.00");
                            transactionAmount.details = transactionDetails;

                            var transaction = new Transaction(); //shows contract of payment, who pays it and what is it paid for
                            transaction.description = "Your order of Quill and Inks Personal Planner";
                            transaction.invoice_number = Guid.NewGuid().ToString(); 
                            transaction.amount = transactionAmount;
                            transaction.item_list = new ItemList //display list of items purchased
                            {
                                items = new List<Item> { planner } //item details

                            };

                            var payer = new Payer(); //who funds the payment
                            payer.payment_method = "paypal";

                            var redirectUrls = new RedirectUrls(); //set of url on paypal page
                            string strPathQuery = HttpContext.Current.Request.Url.PathAndQuery;
                            string strUrl = HttpContext.Current.Request.Url.AbsoluteUri.Replace(strPathQuery, "/");
                            redirectUrls.cancel_url = strUrl + "Members/Cancel.aspx"; //if payment is cancelled
                            redirectUrls.return_url = strUrl + "Members/Complete.aspx"; //if payment is executed

                            var payment = Payment.Create(apiContext, new Payment
                            {
                                intent = "sale",
                                payer = payer,
                                transactions = new List<Transaction> { transaction },
                                redirect_urls = redirectUrls
                            });

                            Session["paymentId"] = payment.id;

                            foreach (var link in payment.links)
                            {
                                if (link.rel.ToLower().Trim().Equals("approval_url"))
                                {
                                    //found the appropriate link, send the user there
                                    Response.Redirect(link.href);
                                }
                            }


                        } //while loop ends here
                    } //data reader ends here

                    sc.Close();
                }
            }
            catch (Exception ex)
            {
                litError.Text = "Error: " + ex;
            }
        }
    }
}
