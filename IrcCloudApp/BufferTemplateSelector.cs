using System.Windows;
using System.Windows.Controls;

namespace IrcCloudApp
{
    
    public class BufferTemplateSelector : DataTemplateSelector
    {
        public DataTemplate NormalBufferTemplate { get; set; }
        public DataTemplate ArchivedBufferTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item,
          DependencyObject container)
        {
            FrameworkElement element = container as FrameworkElement;
            var buffer = item as ChatBuffer;
            if (buffer == null) return null;
            ChatBuffer buf = buffer;
            if (buf.Archived)
            {
                if (element != null) return element.FindResource("ArchivedBufferTemplate") as DataTemplate;
            }
            else if (element != null) return element.FindResource("NormalBufferTemplate") as DataTemplate;
            return null;
        }
    }
}
