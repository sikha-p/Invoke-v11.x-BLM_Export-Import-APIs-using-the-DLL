using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace BLM_Apis
{
    public class Class1
    {

        private static readonly Encoding encoding = Encoding.UTF8;
        public static string generateAuthToken(string CrUrl, string jsonString)
        {
            try
            {
                string apiUrl = CrUrl + "/v1/authentication";
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.ContentLength = jsonString.Length;
                using (System.IO.Stream webStream = request.GetRequestStream())
                using (StreamWriter requestWriter = new StreamWriter(webStream, System.Text.Encoding.ASCII))
                {
                    requestWriter.Write(jsonString);
                }
                WebResponse webResponse = request.GetResponse();
                var streamReader = new StreamReader(webResponse.GetResponseStream());
                var result = streamReader.ReadToEnd();
                string[] arr = result.Split('"');
                string token = arr[3];
                return token;

            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static string exportBot(string CrUrl, string token, string inputJsonString, string filePathToSavePkg)
        {
            try
            {
                string apiUrl = CrUrl + "/v1/blm/export";
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Headers.Add("X-Authorization", token);
                request.ContentLength = inputJsonString.Length;
                using (System.IO.Stream webStream = request.GetRequestStream())
                using (StreamWriter requestWriter = new StreamWriter(webStream, System.Text.Encoding.ASCII))
                {
                    requestWriter.Write(inputJsonString);
                }
                WebResponse webResponse = request.GetResponse();
                Stream s = webResponse.GetResponseStream();
                if (File.Exists(filePathToSavePkg))
                {
                    File.Delete(filePathToSavePkg);
                }
                FileStream os = new FileStream(filePathToSavePkg, FileMode.OpenOrCreate, FileAccess.Write);
                byte[] buff = new byte[102400];
                int c = 0;
                while ((c = s.Read(buff, 0, 10400)) > 0)
                {
                    os.Write(buff, 0, c);
                    os.Flush();
                }
                os.Close();
                s.Close();
                return "Package downloaded to " + filePathToSavePkg;

            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static string importBot(string CrUrl, string token, string inputJsonString)
        {
            try
            {
                var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(inputJsonString);
                Dictionary<string, object> postParameters = new Dictionary<string, object>();
                foreach (var data in dict)
                {
                    if (data.Key == "file")
                    {
                        string filePath = data.Value;
                        string filename = Path.GetFileName(filePath);
                        FileParameter f = new FileParameter(File.ReadAllBytes(filePath), filename, "multipart/form-data");
                        postParameters.Add("file", f);
                    }
                    else
                    {
                        postParameters.Add(data.Key, data.Value);
                    }
                }
                string apiUrl = CrUrl + "/v1/blm/import";
                string response = MultipartFormDataPost(apiUrl, postParameters, token);
                return response;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static string MultipartFormDataPost(string apiUrl, Dictionary<string, object> postParameters, string token)
        {
            try
            {
                string formDataBoundary = String.Format("----------{0:N}", Guid.NewGuid());
                string contentType = "multipart/form-data; boundary=" + formDataBoundary;

                byte[] formData = GetMultipartFormData(postParameters, formDataBoundary);

                return PostForm(apiUrl, contentType, formData, token);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }
        public static string PostForm(string postUrl, string contentType, byte[] formData, string token)
        {
            HttpWebRequest request = WebRequest.Create(postUrl) as HttpWebRequest;

            if (request == null)
            {
                throw new NullReferenceException("request is not a http request");
            }

            // Set up the request properties.
            request.Method = "POST";
            request.ContentType = contentType;
            request.Headers.Add("X-Authorization", token);
            request.ContentLength = formData.Length;


            // Send the form data to the request.
            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(formData, 0, formData.Length);
                requestStream.Close();
            }

            StreamReader sr = new StreamReader(request.GetResponse().GetResponseStream());
            string Result = sr.ReadToEnd();
            return Result;
        }

        private static byte[] GetMultipartFormData(Dictionary<string, object> postParameters, string boundary)
        {
            Stream formDataStream = new System.IO.MemoryStream();
            bool needsCLRF = false;

            foreach (var param in postParameters)
            {
                // Thanks to feedback from commenters, add a CRLF to allow multiple parameters to be added.
                // Skip it on the first parameter, add it to subsequent parameters.
                if (needsCLRF)
                    formDataStream.Write(encoding.GetBytes("\r\n"), 0, encoding.GetByteCount("\r\n"));

                needsCLRF = true;

                if (param.Value is FileParameter)
                {
                    FileParameter fileToUpload = (FileParameter)param.Value;
                    // Add just the first part of this param, since we will write the file data directly to the Stream
                    string header = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"; filename=\"{2}\";\r\nContent-Type: {3}\r\n\r\n",
                        boundary,
                        param.Key,
                        fileToUpload.FileName ?? param.Key,
                        fileToUpload.ContentType ?? "application/octet-stream");

                    formDataStream.Write(encoding.GetBytes(header), 0, encoding.GetByteCount(header));

                    // Write the file data directly to the Stream, rather than serializing it to a string.
                    formDataStream.Write(fileToUpload.File, 0, fileToUpload.File.Length);
                }
                else
                {
                    string postData = string.Format("--{0}\r\nContent-Disposition: form-data; name=\"{1}\"\r\n\r\n{2}",
                        boundary,
                        param.Key,
                        param.Value);
                    formDataStream.Write(encoding.GetBytes(postData), 0, encoding.GetByteCount(postData));
                }
            }

            // Add the end of the request.  Start with a newline
            string footer = "\r\n--" + boundary + "--\r\n";
            formDataStream.Write(encoding.GetBytes(footer), 0, encoding.GetByteCount(footer));

            // Dump the Stream into a byte[]
            formDataStream.Position = 0;
            byte[] formData = new byte[formDataStream.Length];
            formDataStream.Read(formData, 0, formData.Length);
            formDataStream.Close();

            return formData;
        }



        public class FileParameter
        {
            public byte[] File { get; set; }
            public string FileName { get; set; }
            public string ContentType { get; set; }
            public FileParameter(byte[] file) : this(file, null) { }
            public FileParameter(byte[] file, string filename) : this(file, filename, null) { }
            public FileParameter(byte[] file, string filename, string contenttype)
            {
                File = file;
                FileName = filename;
                ContentType = contenttype;
            }

        }
        public class param
        {
            public string file { get; set; }
            public string overwriteOption { get; set; }

            public string productionVersionOption { get; set; }

            public string password { get; set; }
        }
    }
}
