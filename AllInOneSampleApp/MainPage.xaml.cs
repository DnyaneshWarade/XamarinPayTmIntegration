using System;
using System.Collections.Generic;
using Xamarin.Forms;
using AllInOneSDK;
using Newtonsoft.Json;
using System.Net;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace AllInOneSampleApp
{

    public enum PaytmErrors
    {
        SUCCESS,
        FAILURE,
    }


    public static class ReadonlyVariables
    {
        public static readonly string SubscriptionTabRoute = "subscription";
        public static readonly string MainRoute = "//main";
        public static readonly string ProfileTabRoute = "profile";
        public static readonly string ProfileRoute = "//main/" + ProfileTabRoute;
        public static readonly string SubscriptionRoute = "//main/" + SubscriptionTabRoute;
        public static readonly string LoginRoute = "//login";
        public static readonly string RegistrationRoute = "//registration";
        public static readonly string ProfilePlaceholderImage = "ProfilePlaceholder.png";
        public static readonly string ServicePlaceholderImage = "ServicePlaceholder.png";
        public static readonly string SubscriptionTabImage = "Booking.png";
        public static readonly string ServiceTabImage = "Service.png";
        public static readonly string ProfileTabImage = "Profile.png";
        public static readonly string WalletTabImage = "Wallet.png";


        //Paytm related variables
        public static readonly string PAYTM_CALLBACK = "https://securegw-stage.paytm.in/theia/paytmCallback?ORDER_ID=";
        public static readonly string PAYTM_KEY = "k0ZxZSES5XJZ9G@r";
        public static readonly string PAYTM_MID = "qFKjOk51773158003561";
        public static readonly string PAYTM_WEBSITENAME = "WEBSTAGING";
        public static readonly string REQUEST_TYPE = "Payment";

#if DEBUG
        public static readonly string PAYTM_LINK = "https://securegw-stage.paytm.in/theia/api/v1/initiateTransaction?mid=";
#else
		public static readonly string PAYTM_LINK = "https://securegw.paytm.in/theia/api/v1/initiateTransaction?mid=";
#endif

    }



    public class PayTmPayment
    {
        public static Tuple<PaytmErrors, string> GenerateTransactionToken(string amountValueUptoTwoDecimals, string custId, string orderID, string websiteName, string requestType, string currency = "INR")
        {
            try
            {
                Dictionary<string, object> body = new Dictionary<string, object>();
                Dictionary<string, string> head = new Dictionary<string, string>();
                Dictionary<string, object> requestBody = new Dictionary<string, object>();
                Dictionary<string, string> txnAmount = new Dictionary<string, string>();
                Dictionary<string, string> userInfo = new Dictionary<string, string>();
                Dictionary<string, object> UPIPaymentOptions = new Dictionary<string, object>();


                UPIPaymentOptions.Add("mode", "UPI");
                UPIPaymentOptions.Add("channels", new[] { "UPI", "UPIPUSH", "UPIPUSHEXPRESS" });

                txnAmount.Add("value", amountValueUptoTwoDecimals);
                txnAmount.Add("currency", currency);
                userInfo.Add("custId", custId); //userInfo.Add("custId", "Utk_1");


                body.Add("requestType", requestType); //body.Add("requestType", "Payment");
                body.Add("mid", ReadonlyVariables.PAYTM_MID); //body.Add("mid", "qFKjOk51773158003561");
                body.Add("websiteName", websiteName); //body.Add("websiteName", "WEBSTAGING");
                body.Add("orderId", orderID); //body.Add("orderId", "order_1");
                body.Add("txnAmount", txnAmount);
                body.Add("userInfo", userInfo);
                body.Add("callbackUrl", ReadonlyVariables.PAYTM_CALLBACK); //body.Add("callbackUrl", "https://merchant.com/callback");
                body.Add("enablePaymentMode", new object[] { UPIPaymentOptions});


                string paytmChecksum = Paytm.Checksum.generateSignature(JsonConvert.SerializeObject(body), ReadonlyVariables.PAYTM_KEY);

                head.Add("signature", paytmChecksum);

                requestBody.Add("body", body);
                requestBody.Add("head", head);

                string post_data = JsonConvert.SerializeObject(requestBody);

                string url = ReadonlyVariables.PAYTM_LINK + ReadonlyVariables.PAYTM_MID + "&orderId=" + orderID;

                HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);

                webRequest.Method = WebRequestMethods.Http.Post;
                webRequest.ContentType = "application/json";
                webRequest.ContentLength = post_data.Length;

                using (StreamWriter requestWriter = new StreamWriter(webRequest.GetRequestStream()))
                {
                    requestWriter.Write(post_data);
                }

                string responseData = string.Empty;

                using (StreamReader responseReader = new StreamReader(webRequest.GetResponse().GetResponseStream()))
                {
                    responseData = responseReader.ReadToEnd();

                    JObject returnedobject = JObject.Parse(responseData);

                    var resultStatus = returnedobject["body"]["resultInfo"]["resultStatus"].Value<string>();

                    if (string.Compare(resultStatus, "S", true) == 0)
                    {
                        string txnToken = returnedobject["body"]["txnToken"].Value<string>();

                        var resultMessage = returnedobject["body"]["resultInfo"]["resultMsg"].Value<string>();

                        return new Tuple<PaytmErrors, string>(PaytmErrors.SUCCESS, txnToken);
                    }
                    else
                    {
                        string resultReason = null;

                        //either it would be system error or failure
                        var resultMessage = returnedobject["body"]["resultInfo"]["resultMsg"].Value<string>();

                        return new Tuple<PaytmErrors, string>(PaytmErrors.FAILURE, resultReason);
                    }
                }
            }
            catch (Exception exception)
            {
                return new Tuple<PaytmErrors, string>(PaytmErrors.FAILURE, exception.Message);
            }

            return null;
        }
    }



    public partial class MainPage : ContentPage, PaymentCallback
    {
        public string mid { get; set; }
        public string amount { get; set; }
        public string txnToken { get; set; }
        public string message { get; set; }

        public bool isStaging { get; set; }
        public bool restrictAppInvoke { get; set; }
        public bool isApiInProgress { get; set; }
        public string OrderId { get; set; }

        public MainPage()
        {
            mid = "qFKjOk51773158003561";
            amount = "1.00";
            txnToken = "";
            setMessage("");
            isStaging = false;
            restrictAppInvoke = false;
            isApiInProgress = true;
            InitializeComponent();

            BindingContext = this;



        }
        public static string GetUniqueID()
        {
            Random random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string randomStr = new string(Enumerable.Repeat(chars, 6)
                                .Select(s => s[random.Next(s.Length)]).ToArray());
            return randomStr + DateTime.Now.ToString("yyyyMMddHHmmss");
        }

        public void error(string errorMessage)
        {
            setMessage("error : " + errorMessage);
            AllInOnePlugin.DestroyInstance();
        }

        public void success(Dictionary<string, object> dictionary)
        {
            string value = "{ ";
            foreach (string key in dictionary.Keys)
            {
                value += key + " : " + dictionary[key] + " , ";

            }
            value += " }";
            setMessage(value);
            AllInOnePlugin.DestroyInstance();
        }

        void setMessage(string msg)
        {
            message = "Message: -> " + msg;
            OnPropertyChanged(nameof(message));
        }

        void StartTransaction_Clicked(object sender, EventArgs e)
        {
            OrderId = GetUniqueID();

            var payTMTuple = PayTmPayment.GenerateTransactionToken(amount, "9096160813", OrderId, ReadonlyVariables.PAYTM_WEBSITENAME, ReadonlyVariables.REQUEST_TYPE);
            txnToken = payTMTuple.Item2;

            if (txnToken.Equals(""))
            {
                return;
            }
            AllInOnePlugin.startTransaction(OrderId, mid, txnToken, amount, "", isStaging, true, this);
        }
        
        void IsStagingChanged(System.Object sender, Xamarin.Forms.CheckedChangedEventArgs e)
        {
            isStaging = e.Value;
        }
        void AppInvokeRestrictionChanged(System.Object sender, Xamarin.Forms.CheckedChangedEventArgs e)
        {
            restrictAppInvoke = e.Value;
        }
    }
}
