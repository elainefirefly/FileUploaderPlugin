using Java.Util.Concurrent;
using Plugin.FileUploader.Abstractions;
using OkHttp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.Webkit;

namespace Plugin.FileUploader
{
    /// <summary>
    /// Implementation for Feature
    /// </summary>
    public class FileUploadManager : IFileUploader, ICountProgressListener
    {
        public static TimeUnit UploadTimeoutUnit { get; set; } = TimeUnit.Minutes;
        public static long SocketUploadTimeout { get; set; } = 5;
        public static long ConnectUploadTimeout { get; set; } = 5;

        TaskCompletionSource<FileUploadResponse> uploadCompletionSource;
        public event EventHandler<FileUploadResponse> FileUploadCompleted = delegate { };
        public event EventHandler<FileUploadResponse> FileUploadError = delegate { };
        public event EventHandler<FileUploadProgress> FileUploadProgress = delegate { };

        public async Task<FileUploadResponse> UploadFileAsync(string url, FileBytesItem fileItem, IDictionary<string, string> headers = null, IDictionary<string, string> parameters = null, string boundary = null)
        {
           return await UploadFileAsync(url, new FileBytesItem[] { fileItem }, fileItem.Name, headers, parameters,boundary);
        }
        public async Task<FileUploadResponse> UploadFileAsync(string url, FileBytesItem[] fileItems,string tag, IDictionary<string, string> headers = null, IDictionary<string, string> parameters = null, string boundary = null)
        {

            uploadCompletionSource = new TaskCompletionSource<FileUploadResponse>();

            if (fileItems == null || fileItems.Length == 0)
            {
                var fileUploadResponse = new FileUploadResponse("There are no items to upload", -1, tag);
                FileUploadError(this, fileUploadResponse);

                uploadCompletionSource.SetResult(fileUploadResponse);
            }
                
            Task.Run(() =>
            {
                try
                {
                    var requestBodyBuilder = PrepareRequest(parameters,boundary);

                    foreach(var fileItem in fileItems)
                    {
                        var mediaType = MediaType.Parse(GetMimeType(fileItem.Name));

                        if (mediaType == null)
                            mediaType = MediaType.Parse("*/*");

                        
                        RequestBody fileBody = RequestBody.Create(mediaType, fileItem.Bytes);
                        requestBodyBuilder.AddFormDataPart(fileItem.FieldName, fileItem.Name, fileBody);
                    }

                    var resp = MakeRequest(url, tag, requestBodyBuilder, headers);

                    if (!uploadCompletionSource.Task.IsCompleted)
                    {
                        uploadCompletionSource.SetResult(resp);
                    }

                }
                catch (Java.Net.UnknownHostException ex)
                {
                    var fileUploadResponse = new FileUploadResponse("Host not reachable", -1, tag);
                    FileUploadError(this, fileUploadResponse);
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                    uploadCompletionSource.SetResult(fileUploadResponse);
                }
                catch (Java.IO.IOException ex)
                {
                    var fileUploadResponse = new FileUploadResponse(ex.ToString(), -1,tag);
                    FileUploadError(this, fileUploadResponse);
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                    uploadCompletionSource.SetResult(fileUploadResponse);
                }
                catch (Exception ex)
                {
                    var fileUploadResponse = new FileUploadResponse(ex.ToString(), -1, tag);
                    FileUploadError(this, fileUploadResponse);
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                    uploadCompletionSource.SetResult(fileUploadResponse);
                }


            });

            
            return await uploadCompletionSource.Task;
        }
        string GetMimeType(string url)
        {
            string type = "*/*";
            try
            {
                string extension = MimeTypeMap.GetFileExtensionFromUrl(url);
                if (!string.IsNullOrEmpty(extension))
                {
                    type = MimeTypeMap.Singleton.GetMimeTypeFromExtension(extension.ToLower());
                }
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
           
            return type;
        }

        public async Task<FileUploadResponse> UploadFileAsync(string url, FilePathItem fileItem, IDictionary<string, string> headers = null, IDictionary<string, string> parameters = null, string boundary = null)
        {
            return await UploadFileAsync(url, new FilePathItem[] { fileItem }, fileItem.Path, headers, parameters,boundary);
        }
        public async Task<FileUploadResponse> UploadFileAsync(string url, FilePathItem[] fileItems,string tag, IDictionary<string, string> headers = null, IDictionary<string, string> parameters = null, string boundary = null)
        {

            uploadCompletionSource = new TaskCompletionSource<FileUploadResponse>();

            if (fileItems == null || fileItems.Length == 0)
            {
                var fileUploadResponse = new FileUploadResponse("There are no items to upload", -1, tag);
                FileUploadError(this, fileUploadResponse);
                uploadCompletionSource.SetResult(fileUploadResponse);
            }

            Task.Run(() =>
            {
                try
                {
                    
                    var requestBodyBuilder = PrepareRequest(parameters,boundary);

                    foreach (var fileItem in fileItems)
                    {
                        Java.IO.File f = new Java.IO.File(fileItem.Path);
                        string fileAbsolutePath = f.AbsolutePath;

                        RequestBody file_body = RequestBody.Create(MediaType.Parse(GetMimeType(fileItem.Path)), f);
                        var fileName = fileAbsolutePath.Substring(fileAbsolutePath.LastIndexOf("/") + 1);
                        requestBodyBuilder.AddFormDataPart(fileItem.FieldName, fileName, file_body);
                    }

                    var resp=MakeRequest(url, tag, requestBodyBuilder, headers);

                    if (!uploadCompletionSource.Task.IsCompleted)
                    {
                        uploadCompletionSource.SetResult(resp);
                    }
              
                }
                catch (Java.Net.UnknownHostException ex)
                {
                    var fileUploadResponse = new FileUploadResponse("Host not reachable", -1,tag);
                    FileUploadError(this, fileUploadResponse);
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                    uploadCompletionSource.SetResult(fileUploadResponse);
                }
                catch (Java.IO.IOException ex)
                {
                    var fileUploadResponse = new FileUploadResponse(ex.ToString(), -1, tag);
                    FileUploadError(this, fileUploadResponse);
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                    uploadCompletionSource.SetResult(fileUploadResponse);
                }
                catch (Exception ex)
                {
                    var fileUploadResponse = new FileUploadResponse(ex.ToString(), -1, tag);
                    FileUploadError(this, fileUploadResponse);
                    System.Diagnostics.Debug.WriteLine(ex.ToString());
                    uploadCompletionSource.SetResult(fileUploadResponse);
                }
            });

            return await uploadCompletionSource.Task;
        }


        MultipartBuilder PrepareRequest(IDictionary<string, string> parameters = null,string boundary = null)
        {
            MultipartBuilder requestBodyBuilder = null;

            if(string.IsNullOrEmpty(boundary))
            {
                requestBodyBuilder = new MultipartBuilder()
                        .Type(MultipartBuilder.Form);
            }
            else
            {
                requestBodyBuilder = new MultipartBuilder(boundary)
                        .Type(MultipartBuilder.Form);
            }

            if (parameters != null)
            {
                foreach (string key in parameters.Keys)
                {
                    if (parameters[key] != null)
                    {
                        requestBodyBuilder.AddFormDataPart(key, parameters[key]);
                    }
                }
            }
            return requestBodyBuilder;
        }
        FileUploadResponse MakeRequest(string url,string tag,  MultipartBuilder requestBodyBuilder, IDictionary<string, string> headers = null)
        {
            //RequestBody requestBody = requestBodyBuilder.Build();
            CountingRequestBody requestBody = new CountingRequestBody(requestBodyBuilder.Build(),tag,this);
            var requestBuilder = new Request.Builder();

            if (headers != null)
            {
                foreach (string key in headers.Keys)
                {
                    if (!string.IsNullOrEmpty(headers[key]))
                    {
                        requestBuilder = requestBuilder.AddHeader(key, headers[key]);
                    }
                }
            }

            Request request = requestBuilder
                .Url(url)
                .Post(requestBody)
                .Build();

            OkHttpClient client = new OkHttpClient();
            client.SetConnectTimeout(ConnectUploadTimeout, UploadTimeoutUnit); // connect timeout
            client.SetReadTimeout(SocketUploadTimeout, UploadTimeoutUnit);    // socket timeout

            Response response = client.NewCall(request).Execute();
            var responseString = response.Body().String();
            var code = response.Code();
            var fileUploadResponse = new FileUploadResponse(responseString, code, tag);
            if (response.IsSuccessful)
            {

                FileUploadCompleted(this, fileUploadResponse);

                return fileUploadResponse;
            }
            else
            {

                FileUploadError(this, fileUploadResponse);
                return fileUploadResponse;
            }
        }

        public void OnProgress(long bytesWritten, long contentLength)
        {
            var fileUploadProgress = new FileUploadProgress(bytesWritten, contentLength);
            FileUploadProgress(this, fileUploadProgress);
        }

        public void OnError(string tag,string error)
        {
            var fileUploadResponse = new FileUploadResponse(error, -1, tag);
            FileUploadError(this, fileUploadResponse);
            System.Diagnostics.Debug.WriteLine(error);
           

            uploadCompletionSource.SetResult(fileUploadResponse);
        }
    }
}