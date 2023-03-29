using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConsoleLog.Text;
using ITUtility;
using DataAccessUtility;
using Oracle.ManagedDataAccess.Client;

namespace EdorseToEDM
{
    class Program
    {
        private string path = Utility.AppSetting("path");
        private string temp = Utility.AppSetting("temp");
        private string logs = Utility.AppSetting("logs");
        private string[] To = Utility.AppSetting("emailto").Split(',');
        private string[] Bcc = Utility.AppSetting("emailbcc").Split(',');
        private string from = Utility.AppSetting("emailfrom");
        private int limit = Convert.ToInt32(Utility.AppSetting("limit"));
        private string ConnectionString = Utility.AppSetting("ConnectionString");
        private List<EDORSE_MODEL> model = new List<EDORSE_MODEL>();
        private List<string> ErrorFullname = new List<string>();
        private List<rpt_model> rpt = new List<rpt_model>();
        static void Main(string[] args)
        {

            Program pr = new Program();
            long tranID = 0;
            DateTime begindate = DateTime.Now;
            DateTime expdate = begindate.AddHours(2);
            using (BatchTransSvc.BatTransClient client = new BatchTransSvc.BatTransClient())
            {
                try
                {
                    pr.WriteLogs("Add BatchTrans", EnumLogType.Info);
                    client.AddTrans(out tranID, "EdorseToEDM", "batchprocess", begindate, expdate);
                    pr.GET_POLICY();
                }
                catch (Exception ex)
                {
                    pr.WriteLogs(ex.Message, EnumLogType.Error);
                }
                finally
                {
                    pr.WriteLogs("Update BatchTrans", EnumLogType.Info);
                    client.UpdateTrans(tranID, expdate, DateTime.Now, 'C', ""); 
                }
            }
        }

        public void GET_POLICY()
        {
            int i = 0;
            int index = 0;
            string folderday = $"{DateTime.Now.ToString("yyyy", new System.Globalization.CultureInfo("en-US"))}{DateTime.Now.ToString("MM")}{DateTime.Now.ToString("dd")}";
            if (!Directory.Exists(temp))
            {
                Directory.CreateDirectory(temp);
            }
            temp = $"{temp}{folderday}";
            Directory.CreateDirectory(temp);
            if (Directory.Exists(path))
            {

                while (true)
                {
                    i = i + 1;
                    //   var files = Directory.GetFiles(path);
                    DirectoryInfo dir = new DirectoryInfo(path);
                    FileInfo[] files = dir.GetFiles($"*.pdf");
                    if (files == null || files.Length == 0 || i > limit || files.Length-1 < index)
                        break;
                    if (File.Exists(files[index].FullName))
                    {
                        var filestr = files[index].FullName.Split('\\');
                        if (filestr != null)
                        {
                            var filename = filestr.Last();
                            var data = filename.Split('_');
                            if (data != null && data.Length >= 3)
                            {
                                var policyno = (data[2]).Replace(".pdf", "").Replace(".PDF", "");
                                DirectoryInfo hdDirectoryInWhichToSearch = new DirectoryInfo(path);
                                FileInfo[] filesInDir = hdDirectoryInWhichToSearch.GetFiles($"*{policyno}*.pdf");
                                policyno = policyno.PadLeft(8, '0');
                                var datamodel = GETDATA(policyno);
                                if (datamodel == null)
                                {
                                    rpt.Add(new rpt_model { POLICY_NO = policyno, errorflg = "Y", errormessage = $"ไม่พบข้อมูลกรมธรรม์ : {policyno}", filename = String.Join("\n", filesInDir.Select(a => a.Name)) });
                                    index++;
                                    continue;
                                }
                                if (datamodel.PS_ID == null)
                                {
                                    rpt.Add(new rpt_model { POLICY_NO = policyno, errorflg = "Y", errormessage = $"ไม่พบข้อมูลกรมธรรม์ BLA Counter Service  : {policyno}", filename = String.Join("\n", filesInDir.Select(a => a.Name)) });
                                    index++;
                                    continue;
                                }

                                try
                                {
                                    List<byte[]> pdf = new List<byte[]>();
                                    datamodel.filedetail = new List<filedetail>();
                                    foreach (FileInfo foundFile in filesInDir)
                                    {
                                        pdf.Add(File.ReadAllBytes(foundFile.FullName));
                                        datamodel.filedetail.Add(new filedetail { FILE_NAME = foundFile.Name, FULL_FILE_NAME = foundFile.FullName });
                                    }
                                    var allpdf = CombineMultiplePDFs(pdf);
                                    datamodel.file = new DataFile
                                    {
                                        ContentType = "application/pdf",
                                        FileName = $"{policyno}.pdf",
                                        FileData = allpdf
                                    };
                                    long? IMAGE_ID = null;
                                    IMPORTED_TO_EDM(out IMAGE_ID, datamodel, "000000", "ENDORSE");
                                    foreach (var item in datamodel.filedetail)
                                    {
                                        File.Copy(item.FULL_FILE_NAME, $@"{temp}\{item.FILE_NAME}", true);
                                        WriteLogs($@"{datamodel.POLICY_NUMBER}:Copy to {temp}\{item.FILE_NAME}", EnumLogType.Success);
                                        File.Delete(item.FULL_FILE_NAME);
                                        WriteLogs($@"{datamodel.POLICY_NUMBER}:Delete", EnumLogType.Success);
                                    }
                                    rpt.Add(new rpt_model { POLICY_NO = datamodel.POLICY_NUMBER, errorflg = "N", filename = String.Join("\n", datamodel.filedetail.Select(a => a.FILE_NAME).ToList()) });

                                }
                                catch (Exception ex)
                                {
                                    rpt.Add(new rpt_model { POLICY_NO = datamodel.POLICY_NUMBER, errorflg = "Y", errormessage = ex.Message, filename = String.Join("\n", datamodel.filedetail.Select(a => a.FILE_NAME).ToList()) });
                                    WriteLogs($"{datamodel.POLICY_NUMBER}:{ex.Message}", EnumLogType.Error);
                                    index++;
                                }
                            }
                            else
                            {
                                if(data != null)
                                {
                                    rpt.Add(new rpt_model { POLICY_NO = "-", errorflg = "Y", errormessage = "รูปแบบไม่ถูกต้อง", filename = files[index].FullName.Split('\\').LastOrDefault() });
                                    WriteLogs($"ชื่อไฟล์ :{files[index].FullName.Split('\\').LastOrDefault()} :รูปแบบไม่ถูกต้อง", EnumLogType.Error);                                 
                                }
                               index++;
                            }
                        }

                    }

                }
                //รายการที่ไม่สามารถ Importเข้า EDM ได้ : 100 ไฟล์
                var lastdir = new DirectoryInfo(path);
                FileInfo[] countfile = lastdir.GetFiles($"*.pdf");
                var b = GenReport(rpt, $"รายการที่ไม่สามารถ Importเข้า EDM ได้ :{countfile.Length} ไฟล์");
                DataFile att = new DataFile
                {
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    FileData = b,
                    FileName = "รายงานการนำเข้าไฟล์.xlsx",
                };
                SEND_EMAIL(att, To, Bcc, "รายงานผลการนำเข้าไฟล์");
            }

        }

        public EDORSE_MODEL GETDATA(string POLICY_NO)
        {
            OracleConnection connection = new OracleConnection(ConnectionString);
            connection.Open();
            try
            {
                OracleCommand oCmd = new OracleCommand
                {
                    CommandText = @"select pol.*,psj.ps_id,appl.app_no from POLICY.p_policy_identity pol
LEFT JOIN POLICY.p_pos_service_job psj ON psj.policy_id =pol.policy_id 
AND  psj.job_code  IN ('J02','J06','J07','J32','J85')
and psj.fsystem_dt BETWEEN sysdate-365 and sysdate
LEFT JOIN POLICY.P_APPL_ID appl ON appl.POLICY_ID =pol.POLICY_ID
where
pol.policy_number =:IN_POLICY_NO
AND pol.CHANNEL_TYPE ='OD'",
                    CommandType = System.Data.CommandType.Text,
                    Connection = connection
                };
                
                oCmd.Parameters.Add(new OracleParameter("IN_POLICY_NO", POLICY_NO));
                using (var dt = Utility.FillDataTable(oCmd))
                {
                    if (dt.Rows.Count > 0)
                        return dt.AsEnumerable<EDORSE_MODEL>().First();
                    else return null;
                }
            }
            catch (Exception ex)
            {

                throw ex;
            }
            finally
            {
                connection.Close();
            }
        }

        private void IMPORTED_TO_EDM(out long? imageid, EDORSE_MODEL condition, string N_USERID, string CATEGORY)
        {
            using (EasySolutionWcfSvc.EasySolutionSvcClient edm = new EasySolutionWcfSvc.EasySolutionSvcClient())
            {
                imageid = null;
                EasySolutionWcfSvc.CATEGORY_INDEX[] eletter = string.IsNullOrWhiteSpace(condition.CERT_NUMBER) ? new EasySolutionWcfSvc.CATEGORY_INDEX[4] : new EasySolutionWcfSvc.CATEGORY_INDEX[5];
                eletter[0] = new EasySolutionWcfSvc.CATEGORY_INDEX { IndexName = "POLICY_ID", IndexValues = condition.POLICY_ID.ToString() };
                eletter[1] = new EasySolutionWcfSvc.CATEGORY_INDEX { IndexName = "POLICY_NO", IndexValues = condition.POLICY_NUMBER };
                eletter[2] = new EasySolutionWcfSvc.CATEGORY_INDEX { IndexName = "CHANNEL_TYPE", IndexValues = condition.CHANNEL_TYPE };
                eletter[3] = new EasySolutionWcfSvc.CATEGORY_INDEX { IndexName = "APP_NO", IndexValues = condition.APP_NO };
                if (!string.IsNullOrWhiteSpace(condition.CERT_NUMBER))
                    eletter[4] = new EasySolutionWcfSvc.CATEGORY_INDEX { IndexName = "CERT_NO", IndexValues = condition.CERT_NUMBER };
                // ProcessResult pr = new ProcessResult { Successed = true };
                var pr = edm.ImportEs(out imageid, eletter, condition.CHANNEL_TYPE, condition.file, CATEGORY, N_USERID);
                if (!pr.Successed)
                {
                    throw new Exception(pr.ErrorMessage);
                }
            }
        }

        public byte[] CombineMultiplePDFs(List<byte[]> InFiles)
        {
            // Create document object  
            iTextSharp.text.Document PDFdoc = new iTextSharp.text.Document();
            // Create a object of FileStream which will be disposed at the end  
            using (MemoryStream MyFileStream = new System.IO.MemoryStream())
            //using (System.IO.FileStream MyFileStream = new System.IO.FileStream(@"D:\pd.pdf", System.IO.FileMode.Create))
            {
                // Create a PDFwriter that is listens to the Pdf document  
                iTextSharp.text.pdf.PdfCopy PDFwriter = new iTextSharp.text.pdf.PdfCopy(PDFdoc, MyFileStream);
                if (PDFwriter == null)
                {
                    return null;
                }
                // Open the PDFdocument  
                PDFdoc.Open();
                foreach (var fileName in InFiles)
                {
                    // Create a PDFreader for a certain PDFdocument  
                    iTextSharp.text.pdf.PdfReader PDFreader = new iTextSharp.text.pdf.PdfReader(fileName);
                    PDFreader.ConsolidateNamedDestinations();
                    // Add content  
                    for (int i = 1; i <= PDFreader.NumberOfPages; i++)
                    {
                        iTextSharp.text.pdf.PdfImportedPage page = PDFwriter.GetImportedPage(PDFreader, i);
                        PDFwriter.AddPage(page);
                    }
                    iTextSharp.text.pdf.PRAcroForm form = PDFreader.AcroForm;
                    if (form != null)
                    {
                        // PDFwriter.CopyAcroForm(PDFreader);
                    }
                    // Close PDFreader  
                    PDFreader.Close();
                }
                // Close the PDFdocument and PDFwriter  
                PDFwriter.Close();
                PDFdoc.Close();
                return MyFileStream.ToArray();
            }// Disposes the Object of FileStream  

        }

        public void WriteLogs(string message, EnumLogType type)
        {
            Logger Logger = new Logger(logs, true);
            Logger.Prepare();
            if (type == EnumLogType.Info)
                Logger.Logging(message, EnumLogType.Info);
            if (type == EnumLogType.Success)
                Logger.Logging(message, EnumLogType.Success);
            if (type == EnumLogType.Error)
                Logger.Logging(message, EnumLogType.Error);
        }
        public byte[] GenReport(List<rpt_model> rpt_Models, string countfiles)
        {
            if (rpt_Models != null && rpt_Models.Count > 0)
            {
                MemoryStream memory = new MemoryStream();
                rpt_daily rpt = new rpt_daily(rpt_Models.ToArray(), countfiles);
                rpt.ExportToXlsx(memory);
                return memory.ToArray();
            }
            return null;

        }
        private bool SEND_EMAIL(DataFile file, string[] to, string[] bcc, string body)
        {
            ProcessResult pr = new ProcessResult();
            string userid = "003062";
            string emailFrom = from;
            string Subject = "กวาดเอกสารสลักหลังเข้า EDM ";

            using (EmailProviderWcfSvc.EmailProviderClient client = new EmailProviderWcfSvc.EmailProviderClient())
            {
                var param = new EmailProviderWcfSvc.MailParams()
                {
                    Body = body,
                    From = emailFrom,
                    Subject = Subject,
                    IsHtmlBody = true,
                    Receipients = new EmailProviderWcfSvc.MailReceipients()
                    {
                        To = to,
                        //To = new string[1] { "rangsan.chi@bla.co.th"},
                        BCC = bcc
                        //BCC = new string[1] { "anucha.pas@bangkoklife.com" }
                    },
                    FileAttechments = new EmailProviderWcfSvc.File_Upload[1]
                    {
                        new EmailProviderWcfSvc.File_Upload() {
                            ContentType = file.ContentType,
                            FileName = file.FileName,
                            Data = file.FileData
                        }
                    },
                    //ReferenceOptional = new EmailProviderWcfSvc.MailTrackingReferenceOptional
                    //{
                    //CustomeTracking = new EmailProviderWcfSvc.MailCustomeTracking[]
                    //{
                    //    new EmailProviderWcfSvc.MailCustomeTracking
                    //    {
                    //        KeyName="APP_NO",
                    //        KeyValue ="123",
                    //        DataType ="S"
                    //    }
                    //},
                    //Tracking = new EmailProviderWcfSvc.MailBaseTracking[]
                    //{
                    //    new EmailProviderWcfSvc.MailBaseTracking
                    //    {
                    //        PolicyId =POLICY_ID,
                    //    }
                    //}

                    //}
                };
                pr = client.SendMail(param, userid);
                if (!pr.Successed)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }
    }
}
