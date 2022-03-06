// Settings Page

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.Networking.BackgroundTransfer;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Web;
using StoreLib.Models;
using StoreLib.Services;


// 
namespace StoreApp
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SetPage : Page, IDisposable
    {
        // A pointer back to the main page.  This is needed if you want to call methods in MainPage such
        // as NotifyUser()
        public MainPage rootPage = MainPage.Current;
        

       

        private List<DownloadOperation> activeDownloads;
        private CancellationTokenSource cts;

        public SetPage()
        {
            cts = new CancellationTokenSource();

            this.InitializeComponent();

            //this.rootPage = MainPage.Current;

        }

        public void Dispose()
        {
            if (cts != null)
            {
                cts.Dispose();
                cts = null;
            }

            GC.SuppressFinalize(this);
        }

        /*
        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            // An application must enumerate downloads when it gets started to prevent stale downloads/uploads.
            // Typically this can be done in the App class by overriding OnLaunched() and checking for
            // "args.Kind == ActivationKind.Launch" to detect an actual app launch.
            // We do it here in the sample to keep the sample code consolidated.
            await DiscoverActiveDownloadsAsync();
        }


        // Enumerate the downloads that were going on in the background while the app was closed.
        private async Task DiscoverActiveDownloadsAsync()
        {
            activeDownloads = new List<DownloadOperation>();

            IReadOnlyList<DownloadOperation> downloads = null;
            try
            {
                downloads = await BackgroundDownloader.GetCurrentDownloadsAsync();
            }
            catch (Exception ex)
            {
                if (!IsExceptionHandled("Discovery error", ex))
                {
                    throw;
                }
                return;
            }

            Log("Loading background downloads: " + downloads.Count);

            if (downloads.Count > 0)
            {
                List<Task> tasks = new List<Task>();
                foreach (DownloadOperation download in downloads)
                {
                    Log(String.Format(CultureInfo.CurrentCulture,
                        "Discovered background download: {0}, Status: {1}", download.Guid,
                        download.Progress.Status));

                    // Attach progress and completion handlers.
                    tasks.Add(HandleDownloadAsync(download, false));
                }

                // Don't await HandleDownloadAsync() in the foreach loop since we would attach to the second
                // download only when the first one completed; attach to the third download when the second one
                // completes etc. We want to attach to all downloads immediately.
                // If there are actions that need to be taken once downloads complete, await tasks here, outside
                // the loop.
                await Task.WhenAll(tasks);
            }
        }

        
        private async void StartDownload(BackgroundTransferPriority priority)
        {
            // Validating the URI is required since it was received from an untrusted source (user input).
            // The URI is validated by calling Uri.TryCreate() that will return 'false' for strings that are not valid URIs.
            // Note that when enabling the text box users may provide URIs to machines on the intrAnet that require
            // the "Private Networks (Client and Server)" capability.
            Uri source;

            //  "http://tlu.dl.delivery.mp.microsoft.com/filestreamingservice/files/c8a1cc94-cf26-4f6d-ac11-761dcd473fac?P1=1631442089&P2=404&P3=2&P4=hv4aX7GZa5g9mt2wuEkjpRRCgzNmlKsMPsTNgCWh5IJEBOpAwf9Ka2DgnE28deqOh8QjBzJmtHg4%2bRnKMqfrqg%3d%3d";
            //serverAddressField.Text = 
            //         "http://tlu.dl.delivery.mp.microsoft.com/filestreamingservice/files/c8a1cc94-cf26-4f6d-ac11-761dcd473fac?P1=1631450780&P2=404&P3=2&P4=Kd5aTqfOfMgyO2zM5zGy3yMCwV3VAYzD59jXCFg9kTp5FC2QMPW0uEkZ4vEjN9%2f%2bAW661O5mn53nQ0i9DlzfVw%3d%3d";
            //if (!Uri.TryCreate(serverAddressField.Text.Trim(), UriKind.Absolute, out source))

            string serverAddress = "http://localhost";
            if (!Uri.TryCreate(serverAddress.Trim(), UriKind.Absolute, out source))
            {
                rootPage.NotifyUser("Invalid URI.", NotifyType.ErrorMessage);
                return;
            }

            string destination = fileNameField.Text.Trim();

            if (string.IsNullOrWhiteSpace(destination))
            {
                rootPage.NotifyUser("A local file name is required.", NotifyType.ErrorMessage);
                return;
            }

            StorageFile destinationFile;
            try
            {
                StorageFolder picturesLibrary = await KnownFolders.GetFolderForUserAsync(null, KnownFolderId.PicturesLibrary);
                destinationFile = await picturesLibrary.CreateFileAsync(
                    destination,
                    CreationCollisionOption.GenerateUniqueName);
            }
            catch (FileNotFoundException ex)
            {
                rootPage.NotifyUser("Error while creating file: " + ex.Message, NotifyType.ErrorMessage);
                return;
            }

            BackgroundDownloader downloader = new BackgroundDownloader();
            DownloadOperation download = downloader.CreateDownload(source, destinationFile);

            Log(String.Format(CultureInfo.CurrentCulture, "Downloading {0} to {1} with {2} priority, {3}",
                source.AbsoluteUri, destinationFile.Name, priority, download.Guid));

            download.Priority = priority;

            // Attach progress and completion handlers.
            await HandleDownloadAsync(download, true);
        }

        private void StartDownload_Click(object sender, RoutedEventArgs e)
        {
            StartDownload(BackgroundTransferPriority.Default);
        }

        private void StartHighPriorityDownload_Click(object sender, RoutedEventArgs e)
        {
            StartDownload(BackgroundTransferPriority.High);
        }

        private void PauseAll_Click(object sender, RoutedEventArgs e)
        {
            Log("Downloads: " + activeDownloads.Count);

            foreach (DownloadOperation download in activeDownloads)
            {
                // DownloadOperation.Progress is updated in real-time while the operation is ongoing. Therefore,
                // we must make a local copy so that we can have a consistent view of that ever-changing state
                // throughout this method's lifetime.
                BackgroundDownloadProgress currentProgress = download.Progress;

                if (currentProgress.Status == BackgroundTransferStatus.Running)
                {
                    download.Pause();
                    Log("Paused: " + download.Guid);
                }
                else
                {
                    Log(String.Format(CultureInfo.CurrentCulture, "Skipped: {0}, Status: {1}", download.Guid,
                        currentProgress.Status));
                }
            }
        }

        private void ResumeAll_Click(object sender, RoutedEventArgs e)
        {
            Log("Downloads: " + activeDownloads.Count);

            foreach (DownloadOperation download in activeDownloads)
            {
                // DownloadOperation.Progress is updated in real-time while the operation is ongoing. Therefore,
                // we must make a local copy so that we can have a consistent view of that ever-changing state
                // throughout this method's lifetime.
                BackgroundDownloadProgress currentProgress = download.Progress;

                if (currentProgress.Status == BackgroundTransferStatus.PausedByApplication)
                {
                    download.Resume();
                    Log("Resumed: " + download.Guid);
                }
                else
                {
                    Log(String.Format(CultureInfo.CurrentCulture, "Skipped: {0}, Status: {1}", download.Guid,
                        currentProgress.Status));
                }
            }
        }

        private void CancelAll_Click(object sender, RoutedEventArgs e)
        {
            Log("Canceling Downloads: " + activeDownloads.Count);

            cts.Cancel();
            cts.Dispose();

            // Re-create the CancellationTokenSource and activeDownloads for future downloads.
            cts = new CancellationTokenSource();
            activeDownloads = new List<DownloadOperation>();
        }

        // Note that this event is invoked on a background thread, so we cannot access the UI directly.
        private void DownloadProgress(DownloadOperation download)
        {
            // DownloadOperation.Progress is updated in real-time while the operation is ongoing. Therefore,
            // we must make a local copy so that we can have a consistent view of that ever-changing state
            // throughout this method's lifetime.
            BackgroundDownloadProgress currentProgress = download.Progress;

            MarshalLog(String.Format(CultureInfo.CurrentCulture, "Progress: {0}, Status: {1}", download.Guid,
                currentProgress.Status));

            double percent = 100;
            if (currentProgress.TotalBytesToReceive > 0)
            {
                percent = currentProgress.BytesReceived * 100 / currentProgress.TotalBytesToReceive;
            }

            MarshalLog(String.Format(
                CultureInfo.CurrentCulture,
                " - Transferred bytes: {0} of {1}, {2}%",
                currentProgress.BytesReceived,
                currentProgress.TotalBytesToReceive,
                percent));

            if (currentProgress.HasRestarted)
            {
                MarshalLog(" - Download restarted");
            }

            if (currentProgress.HasResponseChanged)
            {
                // We have received new response headers from the server.
                // Be aware that GetResponseInformation() returns null for non-HTTP transfers (e.g., FTP).
                ResponseInformation response = download.GetResponseInformation();
                int headersCount = response != null ? response.Headers.Count : 0;

                MarshalLog(" - Response updated; Header count: " + headersCount);

                // If you want to stream the response data this is a good time to start.
                // download.GetResultStreamAt(0);
            }
        }

        private async Task HandleDownloadAsync(DownloadOperation download, bool start)
        {
            try
            {
                LogStatus("Running: " + download.Guid, NotifyType.StatusMessage);

                // Store the download so we can pause/resume.
                activeDownloads.Add(download);

                Progress<DownloadOperation> progressCallback = new Progress<DownloadOperation>(DownloadProgress);
                if (start)
                {
                    // Start the download and attach a progress handler.
                    await download.StartAsync().AsTask(cts.Token, progressCallback);
                }
                else
                {
                    // The download was already running when the application started, re-attach the progress handler.
                    await download.AttachAsync().AsTask(cts.Token, progressCallback);
                }

                ResponseInformation response = download.GetResponseInformation();

                // GetResponseInformation() returns null for non-HTTP transfers (e.g., FTP).
                string statusCode = response != null ? response.StatusCode.ToString() : String.Empty;

                LogStatus(
                    String.Format(
                        CultureInfo.CurrentCulture,
                        "Completed: {0}, Status Code: {1}",
                        download.Guid,
                        statusCode),
                    NotifyType.StatusMessage);
            }
            catch (TaskCanceledException)
            {
                LogStatus("Canceled: " + download.Guid, NotifyType.StatusMessage);
            }
            catch (Exception ex)
            {
                if (!IsExceptionHandled("Execution error", ex, download))
                {
                    throw;
                }
            }
            finally
            {
                activeDownloads.Remove(download);
            }
        }

        private bool IsExceptionHandled(string title, Exception ex, DownloadOperation download = null)
        {
            WebErrorStatus error = BackgroundTransferError.GetStatus(ex.HResult);
            if (error == WebErrorStatus.Unknown)
            {
                return false;
            }

            if (download == null)
            {
                LogStatus(String.Format(CultureInfo.CurrentCulture, "Error: {0}: {1}", title, error),
                    (NotifyType)NotifyType.ErrorMessage);
            }
            else
            {
                LogStatus(String.Format(CultureInfo.CurrentCulture, "Error: {0} - {1}: {2}", download.Guid, title,
                    error), (NotifyType)NotifyType.ErrorMessage);
            }

            return true;
        }

        // When operations happen on a background thread we have to marshal UI updates back to the UI thread.
        private void MarshalLog(string value)
        {
            var ignore = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                Log(value);
            });
        }

        private void Log(string message)
        {
            //outputField.Text += message + "\r\n";
            outputField.Text = message + "\r\n";
        }

        private void LogStatus(string message, NotifyType type)
        {
            //this.rootPage = MainPage.Current; //RnD

            rootPage.NotifyUser(message, type);
            Log(message);
        }

        // Start Search
        private async void StartSearch_Click(object sender, RoutedEventArgs e)
        {
            string PkgUri;
            string PkgMoniker;
            string PkgFamily;
            string[] monikerSplit;
            Uri PackageUri;
            PackageType PackageType;
            string Digest;
            string Arch;
            DeviceFamily DevFamily;

            string searchQuery;

            // Search zone: Mobile
            DevFamily = DeviceFamily.Mobile;

            // form Search Query
            searchQuery = searchQueryField.Text;

            DisplayCatalogHandler dcathandler = new DisplayCatalogHandler(DCatEndpoint.Production, new Locale(Market.US, Lang.en, true));

            try
            {
                DCatSearch search = await dcathandler.SearchDCATAsync(searchQuery, DevFamily);

                if (search.TotalResultCount > 0)
                {
                    Log($"Result Count: {search.TotalResultCount}");

                    for (int i = 0; i < search.TotalResultCount; i++)
                    {
                        //SuperWriteLine($"Product: {search.Results[i].Products}");
                        Log($"Product title: {search.Results[i].Products[0].Title}");
                        //Log($"Sandbox ID: {search.Results[i].Products[0].SandboxID}");
                        Log($"Product ID: {search.Results[i].Products[0].ProductId}");
                        //Log($"Preferred Sku Id: {search.Results[i].Products[0].PreferredSkuId}");

                        foreach (var PlatformProps in search.Results[i].Products[0].PlatformProperties)
                        {
                            Log($"Platform Props: {PlatformProps}");
                        }

                    }

                    // TODO: use all variants (not only first)

                    DisplayCatalogHandler displayCatalog = new DisplayCatalogHandler(DCatEndpoint.Production, new Locale(Market.US, Lang.en, true));

                    //await displayCatalog.QueryDCATAsync("9wzdncrfj3tj");
                    //await displayCatalog.QueryDCATAsync("9wzdncrdls98");
                    await displayCatalog.QueryDCATAsync(search.Results[0].Products[0].ProductId);

                    //Assert.True(displayCatalog.IsFound);

                    string xml = await FE3Handler.SyncUpdatesAsync(displayCatalog.ProductListing.Product.DisplaySkuAvailabilities[0].Sku.Properties.FulfillmentData.WuCategoryId);
                    IList<string> RevisionIds = new List<string>();
                    IList<string> PackageNames = new List<string>();
                    IList<string> UpdateIDs = new List<string>();

                    FE3Handler.ProcessUpdateIDs(xml, out RevisionIds, out PackageNames, out UpdateIDs);

                    
                    IList<FE3Handler.UrlItem> FileUris = await FE3Handler.GetFileUrlsAsync(UpdateIDs, RevisionIds);

                    //RnD
                    //foreach (string RevId in RevisionIds)
                    //{
                    //    Log($"Revision Id : {RevId}");
                    //}

                    foreach (FE3Handler.UrlItem fileuri in FileUris)
                    {
                        string FIDigest = fileuri.digest;
                        string FileUri = fileuri.url.ToString();
                        
                        //Console.WriteLine($"GetPackagesForNetflix: {fileuri.url}");
                        //Log($"Digest : {FIDigest}");
                        Log($"File Uri : {FileUri}");

                        // Mass download =)
                        AutoDownload(BackgroundTransferPriority.Default, FileUri);
                    }

                }
                else
                {
                    Log($"Result Count: {search.TotalResultCount}");
                }
            }
            catch (Exception ex)
            {
                Log($"Exception: {ex.Message}");
            }

        }//StartSearch_Click end


        private async void AutoDownload(BackgroundTransferPriority priority, string pkgUri)
        {
            // Validating the URI is required since it was received from an untrusted source (user input).
            // The URI is validated by calling Uri.TryCreate() that will return 'false' for strings that are not valid URIs.
            // Note that when enabling the text box users may provide URIs to machines on the intrAnet that require
            // the "Private Networks (Client and Server)" capability.
            Uri source;


            //serverAddressField.Text = pkgUri;


            if (!Uri.TryCreate(pkgUri.Trim(), UriKind.Absolute, out source))
            {
                rootPage.NotifyUser("Invalid URI.", NotifyType.ErrorMessage);
                return;
            }

            string destination = fileNameField.Text.Trim();

            if (string.IsNullOrWhiteSpace(destination))
            {
                rootPage.NotifyUser("A local file name is required.", NotifyType.ErrorMessage);
                return;
            }

            StorageFile destinationFile;
            try
            {
                StorageFolder picturesLibrary = await KnownFolders.GetFolderForUserAsync(null, KnownFolderId.PicturesLibrary);
                destinationFile = await picturesLibrary.CreateFileAsync(
                    destination,
                    CreationCollisionOption.GenerateUniqueName);
            }
            catch (FileNotFoundException ex)
            {
                rootPage.NotifyUser("Error while creating file: " + ex.Message, NotifyType.ErrorMessage);
                return;
            }

            BackgroundDownloader downloader = new BackgroundDownloader();
            DownloadOperation download = downloader.CreateDownload(source, destinationFile);

            Log(String.Format(CultureInfo.CurrentCulture, "Downloading {0} to {1} with {2} priority, {3}",
                source.AbsoluteUri, destinationFile.Name, priority, download.Guid));

            download.Priority = priority;

            // Attach progress and completion handlers.
            await HandleDownloadAsync(download, true);
        }//AutoDownload end
        */

    }// class 

    
}
