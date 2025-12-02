using Graph_Editor.Data.ExecutionRecord;
using Graph_Editor.Pages.MainEditor;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Graph_Editor.Data.SampleQuery
{

    public class SampleQueryItem : INotifyPropertyChanged
    {
        private ObservableCollection<SampleQueryItem> _children;
        private string _name;
        private bool _isExpanded;

        public event PropertyChangedEventHandler PropertyChanged;

        public SampleQueryItem()
        {
        }

        public Guid Id { get; set; } = Guid.NewGuid();

        public enum SampleQueryType { Category, Query };

        public SampleQueryType Type { get; set; }

        public string Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
                NotifyPropertyChanged("Name");
            }
        }

        public string Method { get; set; }

        public string Url { get; set; }

        public Dictionary<string, string> Headers { get; set; }

        public string Body { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool BinaryBodyRequired { get; set; }

        public ObservableCollection<SampleQueryItem> Children
        {
            get
            {
                if (_children == null)
                {
                    _children = new ObservableCollection<SampleQueryItem>();
                }

                return _children;
            }
            set
            {
                _children = value;
            }
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsExpanded
        {
            get
            {
                return _isExpanded;
            }
            set
            {
                _isExpanded = value;
                NotifyPropertyChanged(nameof(IsExpanded));
            }
        }

        public bool IsBuiltIn { get; set; }

        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
