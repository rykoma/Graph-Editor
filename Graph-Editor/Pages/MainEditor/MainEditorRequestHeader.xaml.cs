using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
//using System.Windows.Forms.Design.Behavior;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Graph_Editor.Pages.MainEditor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainEditorRequestHeader : Page
    {
        private ObservableCollection<HeaderItem> Items { get; set; }

        public MainEditorRequestHeader()
        {
            this.InitializeComponent();
            Items = new ObservableCollection<HeaderItem>();
            ListView_HeaderList.ItemsSource = Items;
        }

        public void MakeAllHeadersReadOnly()
        {
            foreach (var item in Items)
            {
                item.IsReadOnly = true;
            }
        }

        public Dictionary<string, string> GetAllHeader()
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in Items)
            {
                headers.Add(item.HeaderName, item.Value);
            }

            return headers;
        }

        public void ReplaceAllHeader(Dictionary<string, string> headers)
        {
            Items.Clear();

            foreach (var item in headers)
            {
                Items.Add(new HeaderItem { HeaderName = item.Key, Value = item.Value, IsReadOnly = true });
            }
        }

        public string ContentTypeHeaderValue
        {
            get
            {
                foreach (var item in Items)
                {
                    if (item.HeaderName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        return item.Value;
                    }
                }

                return null;
            }
        }

        private void Button_AddHeader_Click(object sender, RoutedEventArgs e)
        {
            Items.Add(new HeaderItem { HeaderName = "", Value = "", IsReadOnly = false });
        }

        private void Button_EditHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is HeaderItem item)
            {
                if (item.IsReadOnly)
                {
                    // Change to edit mode
                    item.IsReadOnly = false;
                }
                else
                {
                    // Change to read only mode
                    item.IsReadOnly = true;
                }
            }
        }

        private void Button_DeleteHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is HeaderItem item)
            {
                Items.Remove(item);
            }
        }
    }

    public class HeaderItem : INotifyPropertyChanged
    {
        private bool isReadOnly;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string HeaderName { get; set; }
        public string Value { get; set; }
        
        public bool IsReadOnly
        {
            get { return isReadOnly; }
            set
            {
                if (isReadOnly != value)
                {
                    isReadOnly = value;
                    OnPropertyChanged(nameof(IsReadOnly));
                }
            }
        }
    }
}