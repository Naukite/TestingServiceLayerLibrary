using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestingServiceLayerLibrary;

namespace TestingServiceLayer
{
    class Program
    {
        static void Main(string[] args)
        {
            SLInterface b1Company = new SLInterface("https://andromeda:50000/b1s/v1/");
            bool connected = b1Company.Login(usrName: "manager", pwd: "", db: "SBODEMOES");
            Console.WriteLine($"Resultado de la conexión: {connected}");
            if (!connected)
            {
                Console.ReadLine();
                return;
            }
            //b1Company.GetItems(); 
            //b1Company.GetItem("A00001");
            //b1Company.GetOrders();
            b1Company.AddSaleOrder(cardCode: "C20000", itemCode: "A00001");            
            b1Company.Logout();
            Console.WriteLine("Fin de la ejecución!! ************************");
            Console.ReadLine();
        }
    }
}
