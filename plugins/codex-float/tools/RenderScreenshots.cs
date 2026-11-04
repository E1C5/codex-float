using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using CodexFloat;

// Render the real WPF controls without opening a window or reading live chats.
class RenderScreenshots {
  const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
  static object Get(object target,string name){return target.GetType().GetField(name,Private).GetValue(target);}
  static void Set(object target,string name,object value){target.GetType().GetField(name,Private).SetValue(target,value);}
  static void Call(object target,string name,params object[] args){target.GetType().GetMethod(name,Private).Invoke(target,args);}
  static IEnumerable<DependencyObject> Descendants(DependencyObject item){yield return item;for(int i=0;i<VisualTreeHelper.GetChildrenCount(item);i++)foreach(var child in Descendants(VisualTreeHelper.GetChild(item,i)))yield return child;}
  static void Layout(FrameworkElement item,Size size){item.Measure(size);item.Arrange(new Rect(new Point(),size));item.UpdateLayout();}
  static BitmapSource Capture(FrameworkElement item,Size size){
    Layout(item,size);
    foreach(var node in Descendants(item)){
      var icon=node as StatusIcon;if(icon!=null){typeof(StatusIcon).GetField("changedAt",Private).SetValue(icon,Data.Now-.6);icon.InvalidateVisual();}
      var element=node as FrameworkElement;
      if(element!=null&&element.Name=="RowRoot"){
        element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(1,1,TimeSpan.Zero));
        element.BeginAnimation(FrameworkElement.HeightProperty,new DoubleAnimation(42,42,TimeSpan.Zero));
      }
    }
    item.UpdateLayout();
    var bitmap=new RenderTargetBitmap((int)Math.Ceiling(size.Width*2),(int)Math.Ceiling(size.Height*2),192,192,PixelFormats.Pbgra32);
    bitmap.Render(item);return bitmap;
  }
  static void Save(string path,Size size,Brush background,params object[] layers){
    var visual=new DrawingVisual();using(var drawing=visual.RenderOpen()){
      drawing.DrawRectangle(background,null,new Rect(new Point(),size));
      for(int i=0;i<layers.Length;i+=2){var image=(BitmapSource)layers[i];var at=(Point)layers[i+1];drawing.DrawImage(image,new Rect(at,new Size(image.PixelWidth/2.0,image.PixelHeight/2.0)));}
    }
    var bitmap=new RenderTargetBitmap((int)(size.Width*2),(int)(size.Height*2),192,192,PixelFormats.Pbgra32);bitmap.Render(visual);
    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
    Console.WriteLine(path);
  }
  static Controller Demo(string root,bool light){
    var controller=new Controller(root,true);var window=(Window)Get(controller,"window");
    L.English=true;Call(controller,"ApplyLanguage");Set(controller,"uiScale",1.0);Call(controller,"ApplyScale");Call(controller,"ApplyTheme",light?"light":"dark",false);
    ((Slider)window.FindName("WidthSlider")).Value=380;((Slider)window.FindName("HeightSlider")).Value=370;((Slider)window.FindName("ScaleSlider")).Value=100;((Slider)window.FindName("MaxSlider")).Value=6;Call(controller,"LoadDemo");
    var rows=(List<ThreadRow>)Get(controller,"rows");rows.Clear();
    var samples=new[]{new[]{"Build the desktop widget","working"},new[]{"Review the release settings","question"},new[]{"Restore the server connection","reconnecting"},new[]{"Sync the project files","offline"},new[]{"Update the documentation","completed"}};
    foreach(var sample in samples)rows.Add(new ThreadRow{Id="demo-"+sample[1],TurnId="demo",Title=sample[0],State=sample[1],Detail="Demo data"});
    Call(controller,"UpdateVisible");window.Resources["L.Header"]=L.Get("Header");
    var orb=(QuotaOrb)window.FindName("Orb");orb.BeginAnimation(QuotaOrb.DisplayValueProperty,null);orb.DisplayValue=76;
    return controller;
  }
  [STAThread]static void Main(string[] args){
    string root=Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).FullName;
    string output=args.Length>0?Path.GetFullPath(args[0]):Path.Combine(root,"docs","screenshots");Directory.CreateDirectory(output);
    var application=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
    var dark=Demo(root,false);var darkWindow=(Window)Get(dark,"window");var panel=Capture((FrameworkElement)darkWindow.Content,new Size(384,370));
    Save(Path.Combine(output,"states-dark.png"),new Size(432,418),new SolidColorBrush(Color.FromRgb(24,24,24)),panel,new Point(24,24));
    var light=Demo(root,true);var lightWindow=(Window)Get(light,"window");
    Save(Path.Combine(output,"states-light.png"),new Size(432,418),new SolidColorBrush(Color.FromRgb(241,241,241)),Capture((FrameworkElement)lightWindow.Content,new Size(384,370)),new Point(24,24));
    Call(dark,"UpdateQuotaDetails");var quota=(FrameworkElement)((Popup)darkWindow.FindName("QuotaPopup")).Child;quota.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
    var quotaImage=Capture(quota,quota.DesiredSize);
    Save(Path.Combine(output,"quota-details.png"),new Size(758,418),new SolidColorBrush(Color.FromRgb(24,24,24)),panel,new Point(24,24),quotaImage,new Point(414,418-quotaImage.PixelHeight/2.0-24));
    var settings=(FrameworkElement)((Popup)darkWindow.FindName("SettingsPopup")).Child;settings.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
    var settingsImage=Capture(settings,settings.DesiredSize);
    Save(Path.Combine(output,"settings.png"),new Size(746,Math.Max(418,settingsImage.PixelHeight/2.0+48)),new SolidColorBrush(Color.FromRgb(24,24,24)),panel,new Point(24,24),settingsImage,new Point(414,24));
    var icons=new List<object>();int index=0;
    foreach(string state in new[]{"question","offline","completed"}){
      var mini=Demo(root,false);var window=(Window)Get(mini,"window");Layout((FrameworkElement)window.Content,new Size(384,370));
      var rows=(List<ThreadRow>)Get(mini,"rows");rows.Clear();rows.Add(new ThreadRow{Id="demo-mini",State=state});Set(mini,"pendingCompletion",state=="completed");Call(mini,"CollapseWidget",false,false);
      var surface=(FrameworkElement)window.FindName("MiniSurface");surface.BeginAnimation(UIElement.OpacityProperty,null);surface.Opacity=1;
      icons.Add(Capture((FrameworkElement)window.Content,new Size(64,64)));icons.Add(new Point(24+index*92,24));index++;
    }
    Save(Path.Combine(output,"collapsed.png"),new Size(296,112),new SolidColorBrush(Color.FromRgb(24,24,24)),icons.ToArray());
    application.Shutdown();
  }
}
