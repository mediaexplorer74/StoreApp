//

using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;

namespace StoreApp
{
    public partial class MainPage : Page
    {
        //public const string FEATURE_NAME = "BackgroundTransfer";

        List<Scenario> scenarios = new List<Scenario>
        {
            //new Scenario() { Title="File Download", ClassType=typeof(DownloadZone)},
            
            //new Scenario() { Title="File Upload", ClassType=typeof(Scenario2_Upload)},
          
        };

        internal void NotifyUser(string v, object errorMessage)
        {
            throw new NotImplementedException();
        }
    }

    public class Scenario
    {
        public string Title { get; set; }
        public Type ClassType { get; set; }
    }
}
