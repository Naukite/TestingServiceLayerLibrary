using System;
using System.Threading;
using TestingServiceLayerLibrary.ServiceLayer.SAPB1;

namespace TestingServiceLayerLibrary
{
    public class SLInterface
    {
        #region "ATTRIBUTES"
        private SLService service = null;
        #endregion

        #region "CONSTRUCTOR"
        public SLInterface(string urlSl = @"https://hanab1s03:50000/b1s/v1/")
        {
            service = SLService.GetService(urlSl);
        }
        #endregion

        #region "PRIVATE METHPODS"
        private void ShowException(Exception ex)
        {
            Console.WriteLine(string.Format("Excepción: {0}, Inner Exception Message {1}, Pila: {2}", ex.Message, ex.InnerException == null ? string.Empty : ex.InnerException.Message, ex.StackTrace));
        }
        #endregion

        #region "PUBLIC METHODS"
        public bool Login(string usrName = "EO2", string pwd = "Exp3rt0n3$", string db = "BANKINTER_TEST_1")
        {
            B1Session b1Session = null;
            try
            {
                b1Session = service.LoginServer(usrName, pwd, db);
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
            return b1Session != null;
        }

        public void GetOrders()
        {
            try
            {
                service.GetOrders(pageSize: 0);
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        }

        public void GetItems()
        {
            try
            {
                service.GetItems(pageSize: 0);
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        }

        public void GetBillOfExchangeNos()
        {
            try
            {
                service.GetBillOfExchangeNos();
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        }

        public void GetBusinessPartnerByCardCode(string cardCode)
        {
            try
            {
                service.GetBusinessPartner(cardCode);
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        }


        public void AddSaleOrder(string cardCode = "C0001", string itemCode = "TEST00001")
        {
            try
            {
                Console.WriteLine($"Añadiendo pedido de ventas para el cliente {cardCode} ...");
                service.AddSaleOrder(cardCode, itemCode);
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        }

        public void AddSaleOrders(int nOrders = 5)
        {
            for (int i = 0; i < nOrders; i++)
                AddSaleOrder();
        }

        public void GetItem(string itemCode)
        {
            try
            {
                service.GetItem(itemCode);
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        }

        public void AddItem()
        {
            try
            {
                Item item = service.AddItem();
                if (item == null)
                {
                    Console.WriteLine("Item no añadido");
                    return;
                }
                Console.WriteLine($"Item {item.ItemCode}, {item.ItemName} añadido");
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        }

        public void UpdateItem(string itemCode, string textToAdd)
        {
            try
            {
                service.UpdateItem(itemCode, textToAdd);
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        }

        public void AddItems(int nItem = 5, int delay = 0)
        {
            for (int i = 0; i < nItem; i++)
            {
                AddItem();
                if ((delay > 0) && (i < nItem - 1))
                {
                    Console.WriteLine($"Waiting {delay} miliseconds");
                    Thread.Sleep(delay);
                }
            }
        }

        public void GetAdminInfo()
        {
            try
            {
                service.GetAdminInfo();
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        }

        public void Batch()
        {
            try
            {
                service.BatchSample001();
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        }

        public void Logout()
        {
            try
            {
                service.Logout();
            }
            catch (Exception ex)
            {
                ShowException(ex);
            }
        }
        #endregion
    }
}
