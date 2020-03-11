using Microsoft.Data.OData;
using System;
using System.Collections.Generic;
using System.Data.Services.Client;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using TestingServiceLayerLibrary.ServiceLayer.SAPB1;
using Newtonsoft.Json;
using System.IO;

namespace TestingServiceLayerLibrary
{
    public class SLService
    {
        //The property types when formatting JSON in WCF client.
        enum PropertyType
        {
            SimpleEdmx = 0,
            ComplexType = 1,
            Collection = 2              //Collection of complex types
        }

        #region "ATTRIBUTES"
        private static SLService _service = null;
        private string sessionGuid = string.Empty;
        private string routeIdString = string.Empty;
        private int defaultPagingSizing = 10;
        private ServiceLayer.SAPB1.ServiceLayer serviceContainer = null;
        private string urlSl = string.Empty;
        #endregion

        #region "CONSTRUCTOR"
        private SLService(string urlSl = @"https://hanab1s03:50000/b1s/v1/")
        {
            this.urlSl = urlSl;
            Init();
        }
        #endregion

        #region "PRIVATE METHODS"
        private void Init()
        {
            serviceContainer = new ServiceLayer.SAPB1.ServiceLayer(new Uri(urlSl));
            serviceContainer.Format.UseJson();
            serviceContainer.IgnoreMissingProperties = true;
            //Chance for us to filter propeties using our own logics.
            serviceContainer.Configurations.RequestPipeline.OnEntryStarting((arg) =>
            {
                //For exam: Make all null properties [top level] under entity ignored
                //arg.Entry.Properties = arg.Entry.Properties.Where((prop) => prop.Value != null);                
                //Console.WriteLine($"Dumping entity content before : {JsonConvert.SerializeObject(arg.Entry)}******{Environment.NewLine}");
                if (!(arg.Entity is BusinessPartner))
                {
                    //WCF & .NET rules:
                    //A. All value types are initialized with ZEROs, and reference types with null.
                    //B. For primitive types in WCF entities:
                    //==>non-nullable properties are raw types, nullable types are wrapped to be nullable reference types.
                    arg.Entry.Properties = FilterNullValues(arg.Entry.Properties.ToList(), true);
                }
                //Console.WriteLine($"Dumping entity content After : {JsonConvert.SerializeObject(arg.Entry)}******{Environment.NewLine}");
            });
            serviceContainer.SendingRequest += ServiceContainer_SendingRequest;
            serviceContainer.ReceivingResponse += ServiceContainer_ReceivingResponse;
            serviceContainer.MergeOption = MergeOption.OverwriteChanges;
            ServicePointManager.ServerCertificateValidationCallback += RemoteSSLTLSCertificateValidate;
        }

        private PropertyType GetPropertyType(ODataProperty prop)
        {
            PropertyType retType = PropertyType.SimpleEdmx;
            if (null != prop.Value)
            {
                Type propType = prop.Value.GetType();               //Complex typed property
                switch (propType.Name)
                {
                    case "ODataComplexValue":
                        retType = PropertyType.ComplexType;
                        break;

                    case "ODataCollectionValue":
                        retType = PropertyType.Collection;
                        break;


                    default:
                        break;
                }
            }

            return retType;
        }

        /// <summary>
        /// Create new top level property set, to filter all empty collection, null/zero values if "bIgnore" is true.
        /// </summary>
        /// <param name="listSource"></param>
        /// <param name="bIgnore"></param>
        /// <returns></returns>
        private List<ODataProperty> FilterNullValues(List<ODataProperty> listSource, bool bIgnore = false)
        {
            List<ODataProperty> listResults = new List<ODataProperty>();

            if (bIgnore)
            {
                foreach (ODataProperty prop in listSource)
                {
                    PropertyType retType = GetPropertyType(prop);

                    switch (retType)
                    {
                        case PropertyType.SimpleEdmx:
                            {
                                if (null != prop.Value)
                                    listResults.Add(prop);
                            }
                            break;

                        case PropertyType.ComplexType:
                            {
                                ODataComplexValue complex = RebuildComplexValue((ODataComplexValue)prop.Value);
                                if (complex.Properties.Count() > 0)
                                {
                                    prop.Value = complex;
                                    listResults.Add(prop);
                                }
                            }
                            break;

                        case PropertyType.Collection:
                            {
                                ODataCollectionValue coll = RebuildCollectionValue((ODataCollectionValue)prop.Value);
                                List<ODataComplexValue> listSubs = (List<ODataComplexValue>)coll.Items;
                                if (listSubs.Count > 0)
                                {
                                    prop.Value = coll;
                                    listResults.Add(prop);
                                }
                            }
                            break;


                        default:
                            break;
                    }
                }
            }
            else
            {
                listResults.AddRange(listSource);                           //Original one
            }

            return listResults;
        }


        /// <summary>
        /// Create new ODataCollectionValue from the old, to discard all null/empty values
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private ODataCollectionValue RebuildCollectionValue(ODataCollectionValue source)
        {
            ODataCollectionValue newVal = new ODataCollectionValue();
            newVal.TypeName = source.TypeName;


            List<ODataComplexValue> listComplexValues = new List<ODataComplexValue>();
            foreach (ODataComplexValue complex in source.Items)
            {
                ODataComplexValue comx = RebuildComplexValue(complex);
                listComplexValues.Add(comx);
            }

            newVal.Items = listComplexValues;

            return newVal;
        }

        /// <summary>
        /// Create new ODataComplexValue from the old, to discard all null/zero values
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        private ODataComplexValue RebuildComplexValue(ODataComplexValue source)
        {
            ODataComplexValue newVal = new ODataComplexValue();
            newVal.TypeName = source.TypeName;

            List<ODataProperty> complexSons = source.Properties.ToList();

            //Filter to get new list
            List<ODataProperty> filteredSons = new List<ODataProperty>();
            foreach (ODataProperty prop in complexSons)
            {
                PropertyType retType = GetPropertyType(prop);
                switch (retType)
                {
                    case PropertyType.SimpleEdmx:
                        {
                            if (null != prop.Value)
                            {
                                if (prop.Value.GetType().Name == "Int32")
                                {
                                    //Check the value now.
                                    bool bInclude = false;
                                    try
                                    {
                                        //TODO: You cannot simply do this, potential bugs there maybe.
                                        //Use your own logics the determine if need to ignore ZEORs or not.
                                        int val = Convert.ToInt32(prop.Value);
                                        bInclude = (0 != val);
                                    }
                                    catch (Exception)
                                    {
                                    }

                                    if (bInclude)
                                        filteredSons.Add(prop);
                                }
                                else
                                    filteredSons.Add(prop);
                            }

                        }
                        break;


                    case PropertyType.ComplexType:
                        {
                            //Recursively
                            ODataComplexValue comx = RebuildComplexValue((ODataComplexValue)prop.Value);
                            if (comx.Properties.Count() > 0)
                            {
                                prop.Value = comx;
                                filteredSons.Add(prop);
                            }
                        }
                        break;


                    case PropertyType.Collection:
                        {
                            ODataCollectionValue coll = RebuildCollectionValue((ODataCollectionValue)prop.Value);
                            List<ODataComplexValue> listSubs = (List<ODataComplexValue>)coll.Items;
                            if (listSubs.Count > 0)
                            {
                                prop.Value = coll;
                                filteredSons.Add(prop);
                            }
                        }
                        break;


                    default:
                        break;
                }
            }

            //Re-Assign sons
            newVal.Properties = filteredSons;

            return newVal;
        }

        private void ServiceContainer_SendingRequest(object sender, SendingRequestEventArgs e)
        {
            if (e.Request == null)
                throw new Exception("Failed to intercept the sending request");
            HttpWebRequest request = (HttpWebRequest)e.Request;
            request.Accept = "application/json;odata=minimalmetadata";
            request.KeepAlive = true;                               //keep alive
            request.ServicePoint.Expect100Continue = false;        //content
            request.AllowAutoRedirect = true;
            request.ContentType = "application/json;odata=minimalmetadata;charset=utf8";
            request.Timeout = 10000000;    //number of seconds before considering a request as timeout (consider to change it for batch operations)            
            //                               //This way works to bring additional information with request headers
            if (sessionGuid.Length > 0)
            {
                string strB1Session = "B1SESSION=" + sessionGuid;
                if (routeIdString.Length > 0)
                    strB1Session += "; " + routeIdString;
                e.RequestHeaders.Add("Cookie", strB1Session);
            }
            ////Only works for get requests, but we can always use this, even it will be ignored by other request types.
            e.RequestHeaders.Add("Prefer", "odata.maxpagesize=" + defaultPagingSizing.ToString());
        }

        private string CreateHeaderItem(string strName, string strValue)
        {
            string strFormat = "{0} : {1}\n";
            return string.Format(strFormat, strName, strValue);
        }

        private bool RemoteSSLTLSCertificateValidate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private void InspectResponseStream(Stream stream)
        {
            using (StreamReader sr = new StreamReader(stream))
            {
                string str = JsonConvert.SerializeObject(sr.ReadToEnd());
            }
        }

        private void ServiceContainer_ReceivingResponse(object sender, ReceivingResponseEventArgs e)
        {
            if (null == e.ResponseMessage)
                return;

            //InspectResponseStream(e.ResponseMessage.GetStream());

            string strMessage = e.ResponseMessage.GetHeader("Set-Cookie");
            //Format of the Set-Cookie content in response of login action
            //B1SESSION=146eae44-fc3a-11e3-8000-047d7ba5aff2;HttpOnly;,ROUTEID=.node2; path=/b1s

            //Format of the cookie to be sent in request
            //Cookie: B1SESSION=57a86a60-fc3a-11e3-8000-047d7ba5aff2; ROUTEID=.node1

            if (strMessage != null && strMessage.Length > 0)
            {
                //The ROUTEID information will be returned during login, if sever is configured to be "Clustered" Mode.
                int idx = strMessage.IndexOf("ROUTEID=");
                if (idx > 0)
                {
                    string strSubString = strMessage.Substring(idx);
                    int idxSplitter = strSubString.IndexOf(";");
                    if (idxSplitter > 0)
                    {
                        routeIdString = strSubString.Substring(0, idxSplitter);
                    }
                    else
                    {
                        routeIdString = string.Empty;
                    }
                }
            }
        }

        private Document AddNewOSaleOrder(Document order)
        {
            Document newOrderDoc = null;

            try
            {
                serviceContainer.AddToOrders(order);
                DataServiceResponse response = serviceContainer.SaveChanges();
                if (response != null)
                {
                    ChangeOperationResponse opRes = (ChangeOperationResponse)response.SingleOrDefault();
                    object retDoc = ((System.Data.Services.Client.EntityDescriptor)(opRes.Descriptor)).Entity;
                    if (retDoc != null)
                        newOrderDoc = (Document)retDoc;
                }
            }
            catch (Exception ex)
            {
                //Discard the order from beting tracking
                serviceContainer.Detach(order);
                throw ex;
            }
            return newOrderDoc;
        }

        private int GetNextValueItemCode(string prefix = "TEST")
        {
            defaultPagingSizing = 0;
            var query = serviceContainer.Items.Where(i => i.ItemCode.StartsWith(prefix));
            if (query.Count() == 0)
                return 1;
            return query.ToList().Select(i => Convert.ToInt32(i.ItemCode.Replace(prefix, string.Empty))).Max() + 1;
        }

        private Item CreateItem(int nCode, string prefix = "TEST")
        {
            Item item = new Item();
            item.ItemCode = $"{prefix}{nCode.ToString("00000")}";
            item.ItemName = $"Artículo de test {nCode.ToString("00000")} creado el {DateTime.Now.ToString("dd/MM/yy HH:mm:ss")}";
            item.ItemType = "itItems";
            return item;
        }
        #endregion

        #region "PUBLIC METHODS"
        public static SLService GetService(string urlSl = @"https://hanab1s03:50000/b1s/v1/")
        {
            if (_service == null)
                _service = new SLService(urlSl);
            return _service;
        }

        public B1Session LoginServer(string usrName = "manager", string pwd = @"L4nd3$", string db = @"SBOLANDE")
        {
            B1Session session = null;

            try
            {
                //Discard last login information
                sessionGuid = string.Empty;

                Uri login = new Uri(urlSl + "Login");
                //Use : UriOperationParameter for querying options.
                //Use : BodyOperationParameter for sending JSON body.
                BodyOperationParameter[] body = new BodyOperationParameter[3];
                body[0] = new BodyOperationParameter("UserName", usrName);
                body[1] = new BodyOperationParameter("Password", pwd);
                body[2] = new BodyOperationParameter("CompanyDB", db);

                //Both HTTP & HTTPs protocols are supported.
                session = (B1Session)serviceContainer.Execute<B1Session>(login, "POST", true, body).SingleOrDefault();
                if (null != session)
                {
                    sessionGuid = session.SessionId;
                }
            }
            catch (Exception ex)
            {
                sessionGuid = string.Empty;       //clear the last time's session id
                throw ex;
            }
            return session;
        }

        public void GetOrders(int pageSize = 10)
        {
            defaultPagingSizing = pageSize;
            var query = serviceContainer.Orders.Where(o => o.DocTotal > 1000).
                                                Select(o => new { o.DocEntry, o.CardCode, o.DocTotal, o.Address });
            int n = 0;
            foreach (var doc in query)
            {
                Console.WriteLine($"Documento: {doc.DocEntry} CardCode: {doc.CardCode}, DocTotal: {doc.DocTotal}, Address: {doc.Address}");
                n++;
            }
            Console.WriteLine($"Obtenidos {n} Documentos");          
        }

        public void GetBillOfExchangeNos()
        {
            List<int?> numbers = serviceContainer.IncomingPayments.AsEnumerable().Select(p => p.BillOfExchange.BillOfExchangeNo).ToList();            
            foreach(int? number in numbers)
                Console.WriteLine($"BillOfExchangeNo: {number}");
        }

        public void GetItems(int pageSize = 10)
        {
            defaultPagingSizing = pageSize;
            var query = serviceContainer.Items.Select(i => new { i.ItemCode, i.ItemName });
            int n = 0;
            foreach (var item in query)
            {
                Console.WriteLine($"ItemCode {item.ItemCode}, ItemName {item.ItemName}");
                n++;
            }
            Console.WriteLine($"Obtenidos {n} items");
        }

        public Item AddItem(string prefix = "TEST")
        {
            Item item = CreateItem(GetNextValueItemCode(prefix), prefix);
            try
            {
                serviceContainer.AddToItems(item);
                DataServiceResponse response = serviceContainer.SaveChanges();
                if (response != null)
                {
                    ChangeOperationResponse opRes = (ChangeOperationResponse)response.SingleOrDefault();
                    object retItem = ((System.Data.Services.Client.EntityDescriptor)(opRes.Descriptor)).Entity;
                    if (retItem != null)
                        return (Item)retItem;
                }
            }
            catch (Exception ex)
            {
                serviceContainer.Detach(item);
                throw ex;
            }
            return null;
        }

        public Item UpdateItem(string itemCode, string textToAdd)
        {
            Item item = GetItem(itemCode);
            if (item == null)
                throw new Exception($"Item: {itemCode} Not Found!");
            item.ItemName = $"{item.ItemName} UPD. {textToAdd}";
            serviceContainer.UpdateObject(item);
            DataServiceResponse response = serviceContainer.SaveChanges(SaveChangesOptions.PatchOnUpdate);
            if (response != null)
            {
                ChangeOperationResponse opRes = (ChangeOperationResponse)response.SingleOrDefault();
                object retItem = ((System.Data.Services.Client.EntityDescriptor)(opRes.Descriptor)).Entity;
                if (retItem != null)
                {
                    Console.WriteLine($"Artículo {((Item)retItem).ItemCode} modificado. ItemName: {((Item)retItem).ItemName} ");
                    return (Item)retItem;
                }
            }
            return null;
        }


        public void GetBusinessPartner(string cardCode)
        {
            var query = serviceContainer.BusinessPartners.Where(c => c.CardCode == cardCode);
            if (query.Count() == 0)
            {
                Console.WriteLine($"Cardcode {cardCode} not found");
                return;
            }
            foreach (var bp in query)
                Console.WriteLine($"CardCode: {bp.CardCode}, CardName: {bp.CardName}, Frozen: {bp.Frozen}");
        }

        public Item GetItem(string itemCode = "107023010700002")
        {
            var query = serviceContainer.Items.Where(i => i.ItemCode == itemCode);
            if (query == null)
            {
                Console.WriteLine($"ItemCode {itemCode} not found");
                return null;
            }
            Item item = query.Single();
            Console.WriteLine($"ItemCode: {item.ItemCode}, ItemName {item.ItemName}, Frozen: {item.Frozen}");
            return item;
        }

        public void AddSaleOrder(string cardCode = "C0001", string itemCode = "TEST00001")
        {
            Document order = new ServiceLayer.SAPB1.Document();

            order.CardCode = cardCode;
            order.DocDueDate = DateTime.Today;
            order.DocDate = DateTime.Today;
            order.DocObjectCode = "oOrders";
            order.DocType = "dDocument_Items";
            order.DownPaymentType = "tYES";
            order.InterimType = "0";
            order.RelatedType = -1;

            //Add lines
            DocumentLine newLine = new DocumentLine();
            newLine.ItemCode = itemCode;
            newLine.Quantity = 100;
            order.DocumentLines.Add(newLine);

            //Add this order now
            Document newRetDoc = AddNewOSaleOrder(order);
            if (newRetDoc != null)
                Console.WriteLine($"Añadido documento con DocNum: {newRetDoc.DocNum}");
            else
                Console.WriteLine("No se ha añadido documento");
        }

        public void Logout()
        {
            try
            {
                if (sessionGuid.Length > 0)
                {
                    serviceContainer.Execute(new Uri($"{urlSl}Logout"), "POST", new UriOperationParameter[] { });
                    sessionGuid = string.Empty;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Excepción ejecutando logout. Mensaje {ex.Message}. Inner Exception {ex.InnerException.Message}. Pila: {ex.StackTrace}");
            }
        }

        public void GetAdminInfo()
        {
            serviceContainer.Execute(new Uri($"{urlSl}CompanyService_GetCompanyInfo"), "POST", new OperationParameter[] { });
        }

        public void UpdateCompanyAddress(string address)
        {
            //serviceContainer.Execute(new Uri($"{urlSl}CompanyService_UpdateAdminInfo"), "POST", new BodyOperationParameter[] { 
            //        new BodyOperationParameter("AdminInfo", "")

        }

        public void BatchSample001()
        {
            DataServiceRequest queryItems = new DataServiceRequest<Item>(new Uri($"{urlSl}Items('A00001')"));
            DataServiceResponse batchResponse = serviceContainer.ExecuteBatch(new DataServiceRequest[] { queryItems });
            foreach (QueryOperationResponse response in batchResponse)
            {
                if (response.StatusCode > 299 || response.StatusCode < 200)
                    throw new Exception($"An error ocurred: {response.Error.Message}");
                Console.WriteLine($"Item: {JsonConvert.SerializeObject(response.OfType<Item>().Single())}");
            }
        }
        #endregion
    }
}
