using System;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using System.IO;
using System.Text.RegularExpressions;
using CodexFloat;

class LayoutTests {
  [STAThread]static void Main(){
    var windows=new[]{new Rect(20,20,326,350),new Rect(910,20,326,350),new Rect(20,340,326,350),new Rect(910,340,326,350),new Rect(460,170,326,350),new Rect(1368,66.6667,330,400)};
    int count=0;
    foreach(var window in windows){
      var screen=window.X>1280?new Rect(0,0,1706.6667,1018.6667):new Rect(0,0,1280,720);
      var relative=new Rect(screen.Left-window.Left,screen.Top-window.Top,screen.Width,screen.Height);
      foreach(var popup in new[]{new Size(312,269),new Size(300,430),new Size(420,610)}){
        var points=PopupGeometry.Positions(relative,window.Size,popup);
        if(points.Length==0 && popup.Width==312)throw new Exception("A normal menu should fit next to the widget");
        foreach(var point in points){var rect=new Rect(point,popup);if(!relative.Contains(rect))throw new Exception("Popup extends beyond screen");if(rect.IntersectsWith(new Rect(new Point(0,0),window.Size)))throw new Exception("Popup covers conversation window");count++;}
      }
    }
    if(PopupGeometry.Positions(new Rect(0,0,330,350),new Size(330,350),new Size(312,269)).Length!=0)throw new Exception("No-space case must request resize instead of overlapping");
    Console.WriteLine("Popup layout checks passed: "+count+" valid placements; corners, center, large UI and no-space case.");
    if(Notifications.ColorKey(false,new[]{"offline","question"},true)!="")throw new Exception("Expanded panel must hide notification dot");
    if(Notifications.ColorKey(true,new[]{"offline","question"},true)!="Orange")throw new Exception("Offline notification priority");
    if(Notifications.ColorKey(true,new[]{"question"},true)!="Blue")throw new Exception("Question notification color");
    if(Notifications.ColorKey(true,new[]{"working"},true)!="Success")throw new Exception("Completion notification color");
    if(Notifications.ColorKey(true,new[]{"working"},false)!="")throw new Exception("No notification for ordinary activity");
    string root=Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).FullName;
    string markup=File.ReadAllText(Path.Combine(root,"ui","Main.xaml"));
    foreach(Match match in Regex.Matches(markup,@"DynamicResource L\.([A-Za-z.]+)"))if(!L.Values.ContainsKey(match.Groups[1].Value))throw new Exception("Missing translation "+match.Groups[1].Value);
    var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
    var controller=new Controller(root,true);
    var uiWindow=(Window)typeof(Controller).GetField("window",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
    var content=(FrameworkElement)uiWindow.Content;content.Measure(new Size(330,400));content.Arrange(new Rect(0,0,330,400));content.UpdateLayout();
    ((Button)uiWindow.FindName("EnglishButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if(!L.English || (string)uiWindow.Resources["L.Header"]!="Preview")throw new Exception("English selection did not update resources");
    ((Button)uiWindow.FindName("ChineseButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    if(L.English || (string)uiWindow.Resources["L.Header"]!="状态预览")throw new Exception("Chinese selection did not update resources");
    ((Button)uiWindow.FindName("LightButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    ((Button)uiWindow.FindName("DarkButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    var badge=(System.Windows.Shapes.Ellipse)uiWindow.FindName("NotificationDot");if(badge.Visibility!=Visibility.Collapsed)throw new Exception("Expanded notification dot should be hidden");
    typeof(Controller).GetMethod("CollapseWidget",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,new object[]{false,false});
    if(uiWindow.Width>90||((FrameworkElement)uiWindow.FindName("ExpandedSurface")).Visibility!=Visibility.Collapsed)throw new Exception("Collapsed mode must show only the icon");
    typeof(Controller).GetMethod("ExpandWidget",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(controller,new object[0]);
    if(uiWindow.Width<280||((FrameworkElement)uiWindow.FindName("MiniSurface")).Visibility!=Visibility.Collapsed)throw new Exception("Expanded mode did not restore panel");
    Console.WriteLine("Notification priority, translations, headless XAML startup, language and theme controls passed.");app.Shutdown();
  }
}
