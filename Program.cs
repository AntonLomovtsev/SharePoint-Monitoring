using System;
using System.Net;
using System.Net.Mail;
using System.ServiceProcess;
using System.Xml;
using System.IO;
using Microsoft.Web.Administration;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace SPM
{
    class Program
    {

        static void Main(string[] args)
        {
            string body = "";
            string bodyAppPool = "";
            string bodyService = "";
            string bodyPing = "";
            string bodyHDD = "";
            string bodyHTTP = "";
            string body_CPU_RAM = "";
            string _smtp = "";
            string _mailFrom = "";
            string _mailTo = "";
            string _subject = "Мониторинг фермы SP: ";
            int _PingTimeOut = 250;


            XmlDocument doc = new XmlDocument();
            doc.Load(@"C:\SP_Utils\SPM\SPM.xml");
            XmlNode _services = doc.DocumentElement.SelectSingleNode("/Properties/Services");
            if (_services != null && (_services.ChildNodes.Count > 0))
            {
                bodyService = CheckServices(_services, bodyService);
            }
            XmlNode _AppPools = doc.DocumentElement.SelectSingleNode("/Properties/AppPools");

            if (_AppPools != null && (_AppPools.ChildNodes.Count > 0))
            {
                bodyAppPool = CheckApplicationPools(_AppPools, bodyAppPool);
            }
            XmlNode _Addresses = doc.DocumentElement.SelectSingleNode("/Properties/Addresses");
            _PingTimeOut = Convert.ToInt16(_Addresses.Attributes["PingTimeOut"].InnerText);

            if (_Addresses != null && (_Addresses.ChildNodes.Count > 0))
            {
                bodyPing = PingAddresses(_Addresses, bodyPing, _PingTimeOut);
            }
            XmlNode _HDD = doc.DocumentElement.SelectSingleNode("/Properties/HDD");
            if (_HDD != null && (_HDD.ChildNodes.Count > 0))
            {
                bodyHDD = DriveInfo(_HDD, bodyHDD);
            }
            XmlNode _Links = doc.DocumentElement.SelectSingleNode("/Properties/Links");
            if (_Links != null && (_Links.ChildNodes.Count > 0))
            {
                bodyHTTP = CheckHTTP(_Links, bodyHTTP);
            }
            XmlNode _Performance_Monitor = doc.DocumentElement.SelectSingleNode("/Properties/Performance_Monitor");
            if (_Performance_Monitor != null)
            {
                body_CPU_RAM = Check_CPU_RAM(_Performance_Monitor, body_CPU_RAM);
            }
            
            body = bodyAppPool + bodyService + bodyPing + bodyHDD + bodyHTTP+ body_CPU_RAM;
            XmlNode _configure_SendMail = doc.DocumentElement.SelectSingleNode("/Properties/Configure_SendMail");
            if (_configure_SendMail != null)
            {
                _smtp = _configure_SendMail.Attributes["SMTP"].InnerText;
                _mailFrom = _configure_SendMail.Attributes["MailFrom"].InnerText;
                _mailTo = _configure_SendMail.Attributes["MailTo"].InnerText;
                _subject = _subject + _configure_SendMail.Attributes["Subject"].InnerText;
            }
            if (!String.IsNullOrEmpty(body))
            {
                SendMail(_smtp, _mailFrom, "domen\\login", "password", _mailTo, _subject, body);
            }            

        }

        public static string Check_CPU_RAM(XmlNode _Performance_Monitor, string body_CPU_RAM)
        {
            PerformanceCounter cpuCounter;
            PerformanceCounter ramCounter;
            ulong totalMemory=new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            double freeMemoryRam = ramCounter.NextValue();
            double totalMemoryMB=totalMemory / Math.Pow(1024, 2);
            double percentageFreeRAM = freeMemoryRam / totalMemoryMB * 100;
            int _cpu=Convert.ToInt16(_Performance_Monitor.Attributes["CPU"].InnerText);
            int _ram = Convert.ToInt16(_Performance_Monitor.Attributes["RAM"].InnerText);
            if (percentageFreeRAM < _ram)
            {
                body_CPU_RAM = body_CPU_RAM +"\r\n Свободно MB: " + freeMemoryRam + " Свободно %: " + percentageFreeRAM;
            }
            if (cpuCounter.NextValue() > _cpu)
            {
                body_CPU_RAM = body_CPU_RAM + "\r\n Загрузка CPU: " + cpuCounter.NextValue();
            }
            cpuCounter.Dispose();
            ramCounter.Dispose();
            return body_CPU_RAM;
        }

        public static string CheckHTTP(XmlNode _Links, string bodyHTTP)
        {
            foreach (XmlNode _URL in _Links)
            {
                HttpWebRequest request = null;
                HttpWebResponse response = null;
                try
                {
                    request = (HttpWebRequest)WebRequest.Create(_URL.InnerText);
                    request.UseDefaultCredentials = true;
                    request.PreAuthenticate = true;
                    request.Credentials = CredentialCache.DefaultCredentials;
                    
                    Stopwatch timer = new Stopwatch();
                    timer.Start();
                    response = (HttpWebResponse)request.GetResponse();
                    timer.Stop();
                    TimeSpan timeTaken = timer.Elapsed;
                    //Console.WriteLine("Время отклика " + _URL.InnerText + "=" + timeTaken.TotalSeconds);

                    if (Convert.ToInt16(_URL.Attributes["ResponseTime"].InnerText) < timeTaken.TotalSeconds)
                    {
                        bodyHTTP = bodyHTTP + "\r\n Превышено время отклика " + _URL.InnerText + " = " + timeTaken.TotalSeconds;
                    }
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        bodyHTTP = bodyHTTP + "\r\n Такая страница не найдена " + _URL.InnerText;
                    }
                    response.Close();
                }
                catch (Exception e)
                { bodyHTTP = bodyHTTP + "\r\n Exception: " + e.Message; }
            }
            return bodyHTTP;
        }

        public static string DriveInfo(XmlNode _HDD, string bodyHDD)
        {
            int i = 0;
            DriveInfo[] drives = System.IO.DriveInfo.GetDrives();
            if (drives != null)
            {
                foreach (XmlNode _disc in _HDD)
                {
                    foreach (DriveInfo drive in drives)
                    {
                        try
                        {
                            if (_disc.InnerText.Equals(drive.Name))
                            {
                                i++;
                                int _totalFreeSpace = Convert.ToInt16(_disc.Attributes["TotalFreeSpace"].InnerText);
                                int _percentageFreeSpace = Convert.ToInt16(_disc.Attributes["PercentageFreeSpace"].InnerText);
                                double totalFree =drive.TotalFreeSpace / Math.Pow(1024, 2);
                                double total = drive.TotalSize / Math.Pow(1024, 2);
                                double percentageFreeSpace = (totalFree / total) * 100;
                                if ((_totalFreeSpace > totalFree) || (_percentageFreeSpace > percentageFreeSpace))
                                {
                                    bodyHDD = bodyHDD + "\r\n На диске: " + drive.Name + " меньше " + _totalFreeSpace + " МБ или менее " + _percentageFreeSpace + " % свободного места";
                                }
                                break;
                            }
                        }
                        catch (Exception e)
                        { bodyHDD = bodyHDD + " \r\n Ошибка при проверки диска " + drive.Name + " Exception: " + e.Message; }
                    }
                }
            }
            if (i == 0)
            { bodyHDD = "\r\n Ниодин указанный вами диск не найден"; }

            return bodyHDD;
        }
        
        public static string PingAddresses(XmlNode _Addresses, string bodyPing, int _PingTimeOut)
        {
            foreach (XmlNode _Address in _Addresses)
            {
                try
                {
                    Ping ping = new Ping();
                    PingReply pingReply = ping.Send(_Address.InnerText);
                    //Console.WriteLine(pingReply.RoundtripTime); //время ответа
                    //Console.WriteLine(pingReply.Status);        //статус
                    //Console.WriteLine(pingReply.Address);       //IP

                    if (pingReply.RoundtripTime > _PingTimeOut)
                    {
                        bodyPing = bodyPing + "\r\n Пинг превысел заданное значение" + _Address.InnerText + " ->  Время ответа: " + pingReply.RoundtripTime + " Статус запросв: " + pingReply.Status + " IP-Адрес: " + pingReply.Address;
                    }
                    if (!(pingReply.Status == IPStatus.Success)) //Convert.ToString(pingReply.Status).Equals("Success")
                    {

                        bodyPing = bodyPing + "\r\n Пинг Не завершился успешно! " + _Address.InnerText + " ->  Время ответа: " + pingReply.RoundtripTime + " Статус запроса: " + pingReply.Status + " IP-Адрес: " + pingReply.Address;
                    }
                }
                catch (Exception e)
                {

                    bodyPing = bodyPing + "\r\n Ошибка при пинге сервера: " + _Address.InnerText + " Exception: " + e.Message;
                }
            }
            return bodyPing;
        }

        public static string CheckApplicationPools(XmlNode _AppPools, string bodyAppPool)
        {
            int i = 0;
            ServerManager serverManager = new ServerManager();
            ApplicationPoolCollection applicationPoolCollection = serverManager.ApplicationPools;
            if (applicationPoolCollection != null)
            {
                foreach (XmlNode _AppPool in _AppPools)
                {
                    foreach (ApplicationPool applicationPool in applicationPoolCollection)
                    {
                        if (_AppPool.InnerText.Equals(applicationPool.Name))
                        {
                            i++;
                            if (!(applicationPool.State == ObjectState.Started))
                            {
                                bodyAppPool = bodyAppPool + "\r\n" + applicationPool.Name + ": " + Convert.ToString(applicationPool.State);                                
                            }
                            break;
                        }
                    }
                }
            }
            if (i == 0)
            {
                bodyAppPool = "\r\n Ниодин Application Pool не найден";
            }
            return bodyAppPool;
        }

        private static string CheckServices(XmlNode _services, string bodyService)
        {
            int i = 0;
            ServiceController[] WinServices = ServiceController.GetServices();
            if (WinServices != null)
            {
                foreach (XmlNode _service in _services)
                {   
                    foreach (ServiceController WinService in WinServices)
                    {
                        if (WinService.DisplayName.Equals(_service.InnerText))
                        {
                            i++;
                            if (!Convert.ToString(WinService.Status).Equals("Running"))
                            {                                
                                bodyService = bodyService + "\r\n" + WinService.DisplayName + ": " + Convert.ToString(WinService.Status);                                
                            }
                            break;
                        }
                    }
                }
            }
            if (i == 0)
            {
                bodyService = "Ниодна из служб не найдена";
            }
            return bodyService;
        }

        public static void SendMail(string smtpServer, string from, string userName, string password, string mailto, string caption, string message, string attachFile = null)
        {
            try
            {
                MailMessage mail = new MailMessage();
                mail.From = new MailAddress(from);
                mail.To.Add(new MailAddress(mailto));
                mail.Subject = caption;
                mail.Body = message;
                if (!string.IsNullOrEmpty(attachFile))
                    mail.Attachments.Add(new Attachment(attachFile));
                SmtpClient client = new SmtpClient();
                client.Host = smtpServer;
                client.Port = 25;
                client.EnableSsl = false;
                client.Credentials = new NetworkCredential(userName, password);
                //client.Credentials= CredentialCache.DefaultNetworkCredentials;
                //client.UseDefaultCredentials = true;
                client.DeliveryMethod = SmtpDeliveryMethod.Network;
                client.Send(mail);
                mail.Dispose();
            }
            catch (Exception e)
            {
                throw new Exception("Mail.Send: " + e.Message);
            }

        }
               
    }
}