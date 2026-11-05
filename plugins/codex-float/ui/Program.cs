using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Xml;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]
[assembly: AssemblyVersion("0.2.3.0")]

namespace CodexFloat {
  public static class L {
    public static bool English;
    public static readonly Dictionary<string,string[]> Values=new Dictionary<string,string[]> {
      {"Header",new[]{"工作对话","Working chats"}}, {"Preview",new[]{"状态预览","Preview"}},
      {"EmptyTitle",new[]{"暂无工作中的对话","No chats running"}}, {"EmptyHint",new[]{"任务开始后会出现在这里","New tasks appear here"}},
      {"Remaining",new[]{"剩余额度","Remaining quota"}}, {"Settings",new[]{"设置","Settings"}},
      {"Appearance",new[]{"外观","Appearance"}}, {"Dark",new[]{"深色","Dark"}}, {"Light",new[]{"浅色","Light"}},
      {"Language",new[]{"语言","Language"}}, {"Width",new[]{"宽度","Width"}}, {"Height",new[]{"高度","Height"}},
      {"Scale",new[]{"界面缩放","UI scale"}}, {"Rows",new[]{"可见对话行数","Visible chat rows"}},
      {"Exit",new[]{"退出悬浮窗","Exit widget"}}, {"Refresh",new[]{"刷新","Refresh"}}, {"Refreshing",new[]{"刷新中…","Refreshing…"}},
      {"QuotaTooltip",new[]{"查看额度详情","View quota details"}}, {"LoadingQuota",new[]{"正在读取额度…","Loading quota…"}},
      {"Question",new[]{"问题","Question"}}, {"Offline",new[]{"断联","Offline"}}, {"Reconnecting",new[]{"重连中","Reconnecting"}},
      {"Done",new[]{"完成","Done"}}, {"OpenThread",new[]{"点击打开此对话","Click to open this chat"}},
      {"State.working",new[]{"正在工作","Working"}}, {"State.question",new[]{"等待你的处理","Waiting for your input"}},
      {"State.reconnecting",new[]{"正在重新连接","Reconnecting"}}, {"State.offline",new[]{"连接已中断，正在等待网络恢复","Disconnected; waiting for network"}},
      {"State.completed",new[]{"已完成","Completed"}}, {"State.interrupted",new[]{"已中断","Interrupted"}},
      {"OpenFailed",new[]{"暂时无法打开 Codex，请确认已安装桌面版","Cannot open Codex. Check that the desktop app is installed."}},
      {"NoSpace",new[]{"可用空间不足，请移动或缩小悬浮窗后再试","Move or reduce the widget to make room for this panel."}},
      {"StatusUnavailable",new[]{"暂时无法读取对话状态","Chat status is temporarily unavailable"}},
      {"QuotaUnavailable",new[]{"额度暂时无法读取，请稍后刷新","Quota is unavailable. Refresh in a moment."}},
      {"NoQuotaWindows",new[]{"当前账户未提供额度窗口","This account has no quota windows"}},
      {"Login",new[]{"请在 Codex 中登录 ChatGPT 账户","Sign in with your ChatGPT account in Codex"}},
      {"PythonRequired",new[]{"请先安装 Python 3.10 或更新版本","Python 3.10 or later is required"}},
      {"Collapse",new[]{"收起悬浮窗","Collapse widget"}}, {"Expand",new[]{"短按展开，长按拖动","Click to expand; hold to drag"}},
      {"StartupError",new[]{"悬浮窗启动失败，请查看本地 ui-error.log。","The widget could not start. Check the local ui-error.log."}}
    };
    public static string Get(string key){string[] value;return Values.TryGetValue(key,out value)?value[English?1:0]:key;}
  }

  public static class Notifications {
    public static string ColorKey(bool collapsed,IEnumerable<string> states,bool completed){
      if(!collapsed)return "";var list=states.ToList();
      return list.Contains("offline")?"Orange":list.Contains("question")?"Blue":completed?"Success":"";
    }
  }
  public static class PopupGeometry {
    public static Point[] Positions(Rect work,Size target,Size size){
      work.Inflate(-8,-8);double y=Math.Max(work.Top,Math.Min(work.Bottom-size.Height,target.Height-size.Height)),x=Math.Max(work.Left,Math.Min(work.Right-size.Width,target.Width-size.Width));
      var candidates=new[]{new Point(-size.Width-6,y),new Point(target.Width+6,y),new Point(x,-size.Height-6),new Point(x,target.Height+6)};
      return candidates.Where(p=>work.Contains(new Rect(p,size))).OrderByDescending(p=>p.X<0?-work.Left:p.X>=target.Width?work.Right-target.Width:p.Y<0?-work.Top:work.Bottom-target.Height).ToArray();
    }
  }
  public static class Data {
    public static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 2*1024*1024 };
    public static Dictionary<string,object> Obj(object o) { return o as Dictionary<string,object> ?? new Dictionary<string,object>(); }
    public static object Get(Dictionary<string,object> o,string k) { object v; return o.TryGetValue(k,out v)?v:null; }
    public static string Str(Dictionary<string,object> o,string k,string fallback="") { var v=Get(o,k);return v as string ?? fallback; }
    public static double Num(Dictionary<string,object> o,string k,double fallback=0) { double n;var v=Get(o,k);return v!=null && double.TryParse(Convert.ToString(v,CultureInfo.InvariantCulture),NumberStyles.Float,CultureInfo.InvariantCulture,out n) && !double.IsNaN(n) && !double.IsInfinity(n)?n:fallback; }
    public static bool Bool(Dictionary<string,object> o,string k) { return Get(o,k) is bool && (bool)Get(o,k); }
    public static IEnumerable<object> Arr(Dictionary<string,object> o,string k) { return Get(o,k) as object[] ?? new object[0]; }
    public static Dictionary<string,object> Read(string file) { try { using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream,Encoding.UTF8))return Obj(Json.DeserializeObject(reader.ReadToEnd())); }catch{return new Dictionary<string,object>();} }
    public static void Save(string file,object value) { var tmp=file+"."+Process.GetCurrentProcess().Id+".tmp";File.WriteAllText(tmp,Json.Serialize(value),new UTF8Encoding(false));if(File.Exists(file))File.Replace(tmp,file,null);else File.Move(tmp,file); }
    public static double Now { get { return (DateTime.UtcNow-new DateTime(1970,1,1)).TotalSeconds; } }
  }

  public class ThreadRow : INotifyPropertyChanged {
    public event PropertyChangedEventHandler PropertyChanged;
    string state="working",title="",detail="";
    public string Id {get;set;} public string TurnId {get;set;} public double RemovalAt {get;set;}
    public string Title {get{return title;}set{if(title==value)return;title=value;Changed("Title");Changed("Tooltip");Changed("AccessibleName");}}
    public string State {get{return state;}set{if(state==value)return;state=value;Changed("State");Changed("StateLabel");Changed("Tooltip");Changed("AccessibleName");}}
    public string Detail {get{return detail;}set{if(detail==value)return;detail=value;Changed("Tooltip");Changed("AccessibleName");}}
    public string StateLabel {get{return state=="question"?L.Get("Question"):state=="offline"?L.Get("Offline"):state=="reconnecting"?L.Get("Reconnecting"):state=="completed"?L.Get("Done"):"";}}
    string LocalDetail {get{return L.English?L.Get("State."+state):Detail;}}
    public string Tooltip {get{return Title+"\n"+LocalDetail+"\n"+L.Get("OpenThread");}}
    public string AccessibleName {get{return Title+", "+LocalDetail;}}
    public void RefreshLanguage(){Changed("StateLabel");Changed("Tooltip");Changed("AccessibleName");}
    void Changed(string p){if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs(p));}
  }

  public class StatusIcon : Control {
    public static readonly DependencyProperty StateProperty=DependencyProperty.Register("State",typeof(string),typeof(StatusIcon),new FrameworkPropertyMetadata("working",FrameworkPropertyMetadataOptions.AffectsRender,Changed));
    public string State {get{return (string)GetValue(StateProperty);}set{SetValue(StateProperty,value);}}
    double changedAt=Data.Now,lastPaint; bool attached;
    static Geometry Shape(string value){var geometry=Geometry.Parse(value);geometry.Freeze();return geometry;}
    static readonly Geometry Spin=Shape("M 8,1.8 A 6.2,6.2 0 1 1 2,8");
    static readonly Geometry Question=Shape("M 5.3,5 Q 5.4,2.3 8.2,2.3 Q 11.3,2.3 10.7,5 Q 10.4,6.5 8.2,7.7 L 8.2,9");
    static readonly Geometry[] Waves={Shape("M 6.1,10.1 Q 8,8.8 9.9,10.1"),Shape("M 3.8,7.7 Q 8,4.7 12.2,7.7"),Shape("M 1.5,5.2 Q 8,.7 14.5,5.2")};
    public StatusIcon(){Loaded+=(s,e)=>{if(!attached){CompositionTarget.Rendering+=Frame;attached=true;}};Unloaded+=(s,e)=>{if(attached){CompositionTarget.Rendering-=Frame;attached=false;}};IsHitTestVisible=false;}
    static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e){((StatusIcon)d).changedAt=Data.Now;}
    void Frame(object s,EventArgs e){double now=Data.Now;if(SystemParameters.ClientAreaAnimation&&IsVisible&&now-lastPaint>=1.0/30&&(State=="working"||State=="reconnecting"||(State=="completed"&&now-changedAt<1))){lastPaint=now;InvalidateVisual();}}
    static SolidColorBrush Brush(string s){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(s));}
    protected override void OnRender(DrawingContext d){
      base.OnRender(d);d.PushTransform(new ScaleTransform(ActualWidth/16,ActualHeight/16));
      var pen=new Pen(Foreground??Brush("#ACACAC"),1.65){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
      double t=Data.Now-changedAt;bool motion=SystemParameters.ClientAreaAnimation;
      if(State=="working"){
        d.PushTransform(new RotateTransform(motion?(Data.Now*420)%360:25,8,8));
        d.DrawGeometry(null,pen,Spin);d.Pop();
      }else if(State=="completed"){
        var green=TryFindResource("Success") as Brush??Brush("#7BC99A");var p=new Pen(green,1.9){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};
        double k=motion?Math.Max(0,Math.Min(1,t/.48)):1;
        if(k<.4){d.PushOpacity(1-k/.4);d.PushTransform(new RotateTransform(Data.Now*420%360,8,8));d.DrawGeometry(null,pen,Spin);d.Pop();d.Pop();}
        d.PushOpacity(Math.Min(1,k*3));double a=Math.Min(1,k*2.6),b=Math.Max(0,Math.Min(1,(k-.38)/.62));
        d.DrawLine(p,new Point(3.1,8),new Point(3.1+3.2*a,8+3.1*a));
        if(b>0)d.DrawLine(p,new Point(6.3,11.1),new Point(6.3+6.5*b,11.1-7*b));d.Pop();
      }else if(State=="reconnecting" || State=="offline"){
        if(State=="offline")pen.Brush=TryFindResource("Orange") as Brush??Brush("#E9AF72");
        double phase=motion?(t%1.35)/1.35:1;
        for(int i=0;i<3;i++){
          double alpha=State=="offline"?1:Math.Max(.18,Math.Min(1,(phase-i*.19)*6));
          if(State=="reconnecting"&&phase>.86)alpha*=Math.Max(.18,(1-phase)/.14);
          d.PushOpacity(alpha);
          d.DrawGeometry(null,pen,Waves[i]);d.Pop();
        }
        d.DrawEllipse(pen.Brush,null,new Point(8,12.8),.9,.9);
        if(State=="offline")d.DrawLine(new Pen(pen.Brush,1.6){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},new Point(2,2),new Point(14,14));
      }else if(State=="question"){
        var blue=TryFindResource("Blue") as Brush??Brush("#9CC4FF");d.DrawGeometry(null,new Pen(blue,1.4),Question);d.DrawEllipse(blue,null,new Point(8.2,12),.85,.85);
      }
      d.Pop();
    }
  }

  public class QuotaOrb : Control {
    public static readonly DependencyProperty DisplayValueProperty=DependencyProperty.Register("DisplayValue",typeof(double),typeof(QuotaOrb),new FrameworkPropertyMetadata(0.0,FrameworkPropertyMetadataOptions.AffectsRender));
    double? actual;
    public double DisplayValue {get{return (double)GetValue(DisplayValueProperty);}set{SetValue(DisplayValueProperty,value);}}
    public static Color ColorFor(double p,bool light=false){return (Color)ColorConverter.ConvertFromString(light?(p>=50?"#287B51":p>=20?"#946B1A":p>=10?"#AC5B1F":"#B33F3F"):(p>=50?"#7BC99A":p>=20?"#DABD74":p>=10?"#E9A36C":"#E08080"));}
    public void SetQuota(double? value){if(actual==value)return;actual=value;double target=value??0;BeginAnimation(DisplayValueProperty,new DoubleAnimation(DisplayValue,target,TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation?400:0)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});InvalidateVisual();}
    protected override void OnRender(DrawingContext d){
      base.OnRender(d);d.PushTransform(new ScaleTransform(ActualWidth/44,ActualHeight/44));
      var muted=TryFindResource("Muted") as Brush??Brushes.Gray;var track=TryFindResource("Track") as Brush??Brushes.Gray;
      var color=actual.HasValue?new SolidColorBrush(ColorFor(actual.Value,Equals(TryFindResource("Theme"),"light"))):muted;
      d.DrawEllipse(null,new Pen(track,3.4),new Point(22,22),18.2,18.2);
      double value=Math.Max(0,Math.Min(100,DisplayValue));
      if(value>99.98)d.DrawEllipse(null,new Pen(color,3.4),new Point(22,22),18.2,18.2);
      else if(value>0){double angle=value/100*Math.PI*2;var geo=new StreamGeometry();using(var c=geo.Open()){c.BeginFigure(new Point(22,3.8),false,false);c.ArcTo(new Point(22+Math.Sin(angle)*18.2,22-Math.Cos(angle)*18.2),new Size(18.2,18.2),0,value>50,SweepDirection.Clockwise,true,false);}d.DrawGeometry(null,new Pen(color,3.4){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},geo);}
      string label=actual.HasValue?(actual.Value>0&&actual.Value<1?"<1%":Math.Round(actual.Value,0,MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture)+"%"):"—";
      var text=new FormattedText(label,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(new FontFamily("Segoe UI"),FontStyles.Normal,FontWeights.SemiBold,FontStretches.Normal),11.5,TryFindResource("Text") as Brush??Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);
      d.DrawText(text,new Point(22-text.Width/2,22-text.Height/2));d.Pop();
    }
  }

  public class Controller {
    Window window;Grid header,logoHost,expandedSurface,miniSurface;ItemsControl list;Popup quotaPopup,settingsPopup;QuotaOrb orb;
    readonly ObservableCollection<ThreadRow> displayed=new ObservableCollection<ThreadRow>();
    readonly List<ThreadRow> rows=new List<ThreadRow>();readonly HashSet<string> done=new HashSet<string>();
    readonly string root,dataDir,settingsFile;readonly bool demo;Process collector;
    Dictionary<string,object> settings,quota=new Dictionary<string,object>();
    DateTime lastSnapshot=DateTime.MinValue;bool ready=false,refreshing=false,collapsed=false,transitioning=false,pendingCompletion=false,logoDragged=false,logoHoldArmed=false,popupLayoutQueued=false;double refreshStarted,uiScale=1,expandedLeft,expandedTop,expandedWidth,expandedHeight,collapseDx,collapseDy;Point logoDragStart;string statusError="";int maxVisible=4;DispatcherTimer timer,logoHoldTimer;
    T Find<T>(string name) where T:class{return window.FindName(name) as T;}
    public Controller(string basePath,bool preview){
      root=basePath;demo=preview;dataDir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),preview?"CodexFloatPreview":"CodexFloat");Directory.CreateDirectory(dataDir);settingsFile=Path.Combine(dataDir,"settings.json");settings=Data.Read(settingsFile);
      L.English=Data.Str(settings,"language")=="en";
      string markup=File.ReadAllText(Path.Combine(root,"ui","Main.xaml"),Encoding.UTF8);
      if(!SystemParameters.ClientAreaAnimation)markup=Regex.Replace(markup,"Duration=\"[^\"]+\"","Duration=\"0:0:0\"");
      using(var reader=XmlReader.Create(new StringReader(markup)))window=(Window)XamlReader.Load(reader);
      if(preview){window.Title="Codex Float · Preview";window.ShowInTaskbar=true;}
      header=Find<Grid>("DragHeader");list=Find<ItemsControl>("ThreadList");list.ItemsSource=displayed;
      logoHost=Find<Grid>("LogoHost");expandedSurface=Find<Grid>("ExpandedSurface");miniSurface=Find<Grid>("MiniSurface");
      var logoDocument=new XmlDocument{XmlResolver=null};logoDocument.Load(Path.Combine(root,"assets","chatgpt.svg"));var svgPath=logoDocument.GetElementsByTagName("path")[0];Find<System.Windows.Shapes.Path>("ChatGptPath").Data=Geometry.Parse(svgPath.Attributes["d"].Value);
      orb=Find<QuotaOrb>("Orb");quotaPopup=Find<Popup>("QuotaPopup");settingsPopup=Find<Popup>("SettingsPopup");
      double width=Math.Max(280,Math.Min(520,Data.Num(settings,"width",326))),height=Math.Max(220,Math.Min(640,Data.Num(settings,"height",350)));
      window.Width=width;window.Height=height;maxVisible=(int)Math.Max(1,Math.Min(12,Data.Num(settings,"maxVisible",4)));
      ApplyTheme(Data.Str(settings,"theme","dark"),false);
      ApplyLanguage();
      var area=SystemParameters.WorkArea;window.Left=Data.Num(settings,"left",area.Right-width-22);window.Top=Data.Num(settings,"top",area.Bottom-height-22);Clamp();
      expandedLeft=window.Left;expandedTop=window.Top;expandedWidth=window.Width;expandedHeight=window.Height;
      header.MouseLeftButtonDown+=(s,e)=>{if(e.ButtonState==MouseButtonState.Pressed)DragWidget();};
      Find<Grid>("ConversationRegion").MouseLeftButtonDown+=(s,e)=>{if(e.OriginalSource==s)DragWidget();};
      var logoButton=Find<Button>("LogoButton");
      logoButton.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Space||e.Key==Key.Enter){StopLogoHold();logoDragged=false;logoHoldArmed=false;}};
      logoButton.PreviewMouseLeftButtonDown+=(s,e)=>{StopLogoHold();logoDragStart=e.GetPosition(window);logoDragged=false;logoHoldArmed=false;if(collapsed){logoHoldTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};logoHoldTimer.Tick+=(sender,args)=>{logoHoldTimer.Stop();if(Mouse.LeftButton==MouseButtonState.Pressed&&collapsed&&!transitioning){logoHoldArmed=true;logoButton.Cursor=Cursors.SizeAll;}};logoHoldTimer.Start();}};
      logoButton.PreviewMouseMove+=(s,e)=>{if(e.LeftButton==MouseButtonState.Pressed&&!logoDragged&&(!collapsed||logoHoldArmed)&&(e.GetPosition(window)-logoDragStart).Length>5){logoDragged=true;StopLogoHold();DragWidget();logoButton.Cursor=Cursors.Hand;e.Handled=true;}};
      logoButton.PreviewMouseLeftButtonUp+=(s,e)=>StopLogoHold();logoButton.LostMouseCapture+=(s,e)=>StopLogoHold();
      logoButton.Click+=(s,e)=>{if(!logoDragged&&!logoHoldArmed&&!transitioning){if(collapsed)ExpandWidget();else CollapseWidget(true,false);}logoDragged=false;logoHoldArmed=false;};
      list.AddHandler(Button.ClickEvent,new RoutedEventHandler(ThreadClick));
      ConfigurePopup(quotaPopup);ConfigurePopup(settingsPopup);
      window.LocationChanged+=(s,e)=>SchedulePopupLayout();window.SizeChanged+=(s,e)=>SchedulePopupLayout();
      Find<Button>("QuotaButton").Click+=(s,e)=>{settingsPopup.IsOpen=false;UpdateQuotaDetails();TogglePopup(quotaPopup);};
      Find<Button>("SettingsButton").Click+=(s,e)=>{quotaPopup.IsOpen=false;TogglePopup(settingsPopup);};
      Find<Button>("RefreshButton").Click+=(s,e)=>{File.WriteAllText(Path.Combine(dataDir,"refresh.flag"),DateTime.UtcNow.Ticks.ToString());refreshing=true;refreshStarted=Data.Now;Find<Button>("RefreshButton").IsEnabled=false;Find<Button>("RefreshButton").Content=L.Get("Refreshing");UpdateQuotaDetails();};
      Find<Button>("DarkButton").Click+=(s,e)=>{ApplyTheme("dark",true);SaveSettings();};
      Find<Button>("LightButton").Click+=(s,e)=>{ApplyTheme("light",true);SaveSettings();};
      Find<Button>("ChineseButton").Click+=(s,e)=>{L.English=false;ApplyLanguage();SaveSettings();};
      Find<Button>("EnglishButton").Click+=(s,e)=>{L.English=true;ApplyLanguage();SaveSettings();};
      Find<Button>("ExitButton").Click+=(s,e)=>window.Close();
      SetupSlider("Width",width,(v)=>{window.Width=v;Clamp();});SetupSlider("Height",height,(v)=>{window.Height=v;Clamp();});
      uiScale=Math.Max(.8,Math.Min(1.4,Data.Num(settings,"scale",1)));ApplyScale();
      SetupSlider("Scale",uiScale*100,(v)=>{uiScale=v/100;ApplyScale();UpdateVisible();});
      SetupSlider("Max",maxVisible,(v)=>{maxVisible=(int)v;UpdateVisible();});
      window.Closed+=(s,e)=>{if(timer!=null)timer.Stop();SaveSettings();if(collector!=null&&!collector.HasExited){try{collector.Kill();collector.WaitForExit(2000);}catch{}}Application.Current.Shutdown();};
      window.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){quotaPopup.IsOpen=false;settingsPopup.IsOpen=false;}if(demo&&e.Key==Key.F8){var row=rows.FirstOrDefault(r=>r.State=="working");if(row!=null){row.State="completed";row.RemovalAt=Data.Now+1.3;UpdateVisible();}}};
      window.Loaded+=(s,e)=>{ready=true;window.Opacity=0;if(demo)LoadDemo();else StartCollector();timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(350)};timer.Tick+=Tick;timer.Start();Tick(null,EventArgs.Empty);if(Data.Bool(settings,"collapsed"))CollapseWidget(false,true);window.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation?180:0)));};
    }
    string SliderSuffix(string name){return name=="Max"?(L.English?" rows":" 行"):name=="Scale"?"%":" px";}
    void SetupSlider(string name,double value,Action<double> change){var slider=Find<Slider>(name+"Slider");slider.Value=value;Find<TextBlock>(name+"Value").Text=((int)value)+SliderSuffix(name);slider.ValueChanged+=(s,e)=>{Find<TextBlock>(name+"Value").Text=((int)e.NewValue)+SliderSuffix(name);change(e.NewValue);if(ready)SaveSettings();};}
    void ApplyLanguage(){
      foreach(var pair in L.Values)window.Resources["L."+pair.Key]=L.Get(pair.Key);window.Resources["L.Header"]=L.Get(demo?"Preview":"Header");
      window.Title="Codex Float"+(demo?" · Preview":"");
      foreach(var row in rows)row.RefreshLanguage();
      foreach(string name in new[]{"Width","Height","Scale","Max"})Find<TextBlock>(name+"Value").Text=((int)Find<Slider>(name+"Slider").Value)+SliderSuffix(name);
      Find<Button>("ChineseButton").Background=L.English?Brushes.Transparent:(Brush)window.Resources["Hover"];Find<Button>("EnglishButton").Background=L.English?(Brush)window.Resources["Hover"]:Brushes.Transparent;
      Find<Button>("ChineseButton").FontWeight=L.English?FontWeights.Normal:FontWeights.SemiBold;Find<Button>("EnglishButton").FontWeight=L.English?FontWeights.SemiBold:FontWeights.Normal;
      Find<Button>("RefreshButton").Content=L.Get(refreshing?"Refreshing":"Refresh");Find<TextBlock>("EmptyHint").Text=L.Get(statusError==""?"EmptyHint":"StatusUnavailable");
      UpdateQuotaSummary();if(quotaPopup.IsOpen)UpdateQuotaDetails();UpdateBadge();
    }
    string WindowLabel(Dictionary<string,object> value,bool full){double minutes=Data.Num(value,"minutes");if(!L.English)return full?Data.Str(value,"label"):Data.Str(value,"label").Replace("额度","");if(minutes==300)return full?"5-hour limit":"5-hour";if(minutes==10080)return full?"Weekly limit":"Weekly";return minutes%60==0?(minutes/60).ToString("0")+"-hour"+(full?" limit":""):minutes.ToString("0")+"-minute"+(full?" limit":"");}
    string QuotaMessage(){var message=Data.Str(quota,"message");if(!L.English)return message;if(message.Contains("正在读取"))return L.Get("LoadingQuota");if(message.Contains("登录"))return L.Get("Login");if(message.Contains("未提供"))return L.Get("NoQuotaWindows");return message==""?"":L.Get("QuotaUnavailable");}
    void UpdateQuotaSummary(){var first=Data.Arr(quota,"windows").Select(Data.Obj).OrderBy(q=>Data.Num(q,"minutes",double.MaxValue)).FirstOrDefault();bool ok=Data.Bool(quota,"ok")&&first!=null;orb.SetQuota(ok?(double?)Data.Num(first,"remaining"):null);Find<TextBlock>("QuotaLabel").Text=ok?WindowLabel(first,false):(quota.Count==0?L.Get("LoadingQuota"):QuotaMessage());}
    Rect WorkRelative(){try{Point origin=window.PointToScreen(new Point(0,0));var screen=System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)origin.X,(int)origin.Y));var a=screen.WorkingArea;var source=PresentationSource.FromVisual(window);var m=source.CompositionTarget.TransformFromDevice;return new Rect((a.Left-origin.X)*m.M11,(a.Top-origin.Y)*m.M22,a.Width*m.M11,a.Height*m.M22);}catch{var a=SystemParameters.WorkArea;return new Rect(a.Left-window.Left,a.Top-window.Top,a.Width,a.Height);}}
    void Clamp(){var a=WorkRelative();window.Width=Math.Min(window.Width,a.Width-16);window.Height=Math.Min(window.Height,a.Height-16);if(a.Left>0)window.Left+=a.Left+8;else if(a.Right<window.Width)window.Left+=a.Right-window.Width-8;if(a.Top>0)window.Top+=a.Top+8;else if(a.Bottom<window.Height)window.Top+=a.Bottom-window.Height-8;}
    void SaveSettings(){if(!ready)return;try{if(!collapsed){expandedLeft=window.Left;expandedTop=window.Top;expandedWidth=window.Width;expandedHeight=window.Height;}settings["left"]=expandedLeft;settings["top"]=expandedTop;settings["width"]=expandedWidth;settings["height"]=expandedHeight;settings["maxVisible"]=maxVisible;settings["scale"]=uiScale;settings["language"]=L.English?"en":"zh-CN";settings["collapsed"]=collapsed;if(collapsed){settings["miniLeft"]=window.Left;settings["miniTop"]=window.Top;}Data.Save(settingsFile,settings);}catch{}}
    void ApplyScale(){Find<Grid>("MainGrid").LayoutTransform=new ScaleTransform(uiScale,uiScale);foreach(var p in new[]{quotaPopup,settingsPopup})((FrameworkElement)p.Child).LayoutTransform=new ScaleTransform(uiScale,uiScale);logoHost.LayoutTransform=new ScaleTransform(uiScale,uiScale);if(!collapsed)logoHost.Margin=new Thickness(10,11*uiScale,14*uiScale,0);SchedulePopupLayout();}
    void DragWidget(){if(transitioning)return;try{window.DragMove();Clamp();if(collapsed){expandedLeft=window.Left-collapseDx;expandedTop=window.Top-collapseDy;}SaveSettings();}catch(InvalidOperationException){}}
    void StopLogoHold(){if(logoHoldTimer!=null)logoHoldTimer.Stop();Find<Button>("LogoButton").Cursor=Cursors.Hand;}
    void Later(int milliseconds,Action action){if(!SystemParameters.ClientAreaAnimation||milliseconds<=0){action();return;}var clock=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};clock.Tick+=(s,e)=>{clock.Stop();action();};clock.Start();}
    void RotateLogo(double target,bool animate=true){var rotate=(RotateTransform)Find<Canvas>("LogoRotor").RenderTransform;double from=rotate.Angle;rotate.BeginAnimation(RotateTransform.AngleProperty,null);rotate.Angle=target;if(animate&&SystemParameters.ClientAreaAnimation)rotate.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(from,target,TimeSpan.FromMilliseconds(240)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}});}
    void SurfaceAnimation(bool show){var scale=(ScaleTransform)expandedSurface.RenderTransform;scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);scale.ScaleX=scale.ScaleY=show ? .92 : 1;double duration=SystemParameters.ClientAreaAnimation?240:0;var easing=new CubicEase{EasingMode=EasingMode.EaseOut};expandedSurface.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(show?0:1,show?1:0,TimeSpan.FromMilliseconds(duration)){EasingFunction=easing});scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(show ? .92 : 1,show?1:.92,TimeSpan.FromMilliseconds(duration)){EasingFunction=easing});scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(show ? .92 : 1,show?1:.92,TimeSpan.FromMilliseconds(duration)){EasingFunction=easing});}
    void CollapseWidget(bool animate,bool restore){
      if(transitioning||collapsed)return;quotaPopup.IsOpen=false;settingsPopup.IsOpen=false;window.UpdateLayout();expandedLeft=window.Left;expandedTop=window.Top;expandedWidth=window.Width;expandedHeight=window.Height;
      Point anchor=logoHost.TranslatePoint(new Point(logoHost.ActualWidth/2,logoHost.ActualHeight/2),window);double size=Math.Ceiling(44*uiScale)+20;collapseDx=anchor.X-size/2;collapseDy=anchor.Y-size/2;double left=expandedLeft+collapseDx,top=expandedTop+collapseDy;if(restore){left=Data.Num(settings,"miniLeft",left);top=Data.Num(settings,"miniTop",top);}
      expandedSurface.RenderTransformOrigin=new Point(Math.Max(0,Math.Min(1,(anchor.X-10)/Math.Max(1,expandedWidth-20))),Math.Max(0,Math.Min(1,(anchor.Y-10)/Math.Max(1,expandedHeight-20))));
      collapsed=true;transitioning=true;expandedSurface.IsHitTestVisible=false;RotateLogo(60,animate);if(animate)SurfaceAnimation(false);
      Action finish=()=>{expandedSurface.Visibility=Visibility.Collapsed;window.Width=window.Height=size;window.Left=left;window.Top=top;logoHost.Width=logoHost.Height=44;logoHost.HorizontalAlignment=HorizontalAlignment.Center;logoHost.VerticalAlignment=VerticalAlignment.Center;logoHost.Margin=new Thickness(0);miniSurface.Visibility=Visibility.Visible;miniSurface.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(animate&&SystemParameters.ClientAreaAnimation?120:0)));Clamp();transitioning=false;UpdateBadge();SaveSettings();};
      if(animate)Later(240,finish);else finish();
    }
    void ExpandWidget(){
      if(transitioning||!collapsed)return;transitioning=true;double left=window.Left-collapseDx,top=window.Top-collapseDy;collapsed=false;pendingCompletion=false;
      miniSurface.Visibility=Visibility.Collapsed;window.Width=expandedWidth;window.Height=expandedHeight;window.Left=left;window.Top=top;logoHost.Width=logoHost.Height=28;logoHost.HorizontalAlignment=HorizontalAlignment.Right;logoHost.VerticalAlignment=VerticalAlignment.Top;logoHost.Margin=new Thickness(10,11*uiScale,14*uiScale,0);expandedSurface.Visibility=Visibility.Visible;Clamp();RotateLogo(0);SurfaceAnimation(true);
      foreach(var row in rows)if(row.State=="completed")row.RemovalAt=Data.Now+1.4;displayed.Clear();UpdateVisible();UpdateBadge();Later(240,()=>{expandedSurface.IsHitTestVisible=true;transitioning=false;SaveSettings();});
    }
    void UpdateBadge(){if(window==null||logoHost==null)return;string key=Notifications.ColorKey(collapsed,rows.Select(r=>r.State),pendingCompletion);var dot=Find<System.Windows.Shapes.Ellipse>("NotificationDot");dot.Visibility=key==""?Visibility.Collapsed:Visibility.Visible;if(key!="")dot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty,key);var button=Find<Button>("LogoButton");button.ToolTip=L.Get(collapsed?"Expand":"Collapse")+(key==""?"":"\n"+L.Get(key=="Orange"?"Offline":key=="Blue"?"Question":"Done"));AutomationProperties.SetName(button,L.Get(collapsed?"Expand":"Collapse"));}
    CustomPopupPlacement[] PopupPositions(Size size,Size target,Point offset){
      return PopupGeometry.Positions(WorkRelative(),target,size).Select(p=>new CustomPopupPlacement(p,PopupPrimaryAxis.None)).ToArray();
    }
    void ConfigurePopup(Popup popup){popup.PlacementTarget=window;popup.Placement=PlacementMode.Custom;popup.CustomPopupPlacementCallback=PopupPositions;}
    void SchedulePopupLayout(){
      if(popupLayoutQueued||transitioning||quotaPopup==null||settingsPopup==null||(!quotaPopup.IsOpen&&!settingsPopup.IsOpen))return;
      popupLayoutQueued=true;window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{try{if(quotaPopup.IsOpen)PlacePopup(quotaPopup);if(settingsPopup.IsOpen)PlacePopup(settingsPopup);}finally{popupLayoutQueued=false;}}));
    }
    void TogglePopup(Popup popup){
      if(popup.IsOpen){popup.IsOpen=false;return;}PlacePopup(popup);
    }
    void PlacePopup(Popup popup){
      var frame=(FrameworkElement)popup.Child;bool isQuota=popup==quotaPopup;var scroll=Find<ScrollViewer>(isQuota?"QuotaPopupScroll":"SettingsPopupScroll");var work=WorkRelative();
      frame.Width=isQuota?292:280;frame.MaxWidth=Math.Max(160,work.Width/uiScale-36);frame.MaxHeight=Math.Max(100,work.Height/uiScale-36);scroll.MaxHeight=Math.Max(64,frame.MaxHeight-32);
      frame.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));var size=frame.DesiredSize;var target=new Size(window.ActualWidth,window.ActualHeight);
      if(PopupPositions(size,target,new Point()).Length==0){double side=Math.Max(-work.Left,work.Right-target.Width)-20;if(side>=190*uiScale){frame.Width=Math.Min(frame.Width,side/uiScale-20);frame.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));size=frame.DesiredSize;}}
      if(PopupPositions(size,target,new Point()).Length==0){double vertical=Math.Max(-work.Top,work.Bottom-target.Height)-20;if(vertical>=120*uiScale){frame.MaxHeight=vertical/uiScale-20;scroll.MaxHeight=Math.Max(60,frame.MaxHeight-32);frame.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));size=frame.DesiredSize;}}
      if(PopupPositions(size,target,new Point()).Length==0){window.Left+=work.Left+8;Clamp();frame.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));size=frame.DesiredSize;}
      if(PopupPositions(size,target,new Point()).Length>0){popup.IsOpen=true;popup.HorizontalOffset=popup.HorizontalOffset==0?.01:0;}
      else {popup.IsOpen=false;(isQuota?Find<Button>("QuotaButton"):Find<Button>("SettingsButton")).ToolTip=L.Get("NoSpace");}
    }
    void ApplyTheme(string theme,bool animate){
      settings["theme"]=theme;bool light=theme=="light";window.Resources["Theme"]=theme;
      string[] keys={"Surface","Text","Muted","Hover","Line","Blue","BlueBg","Orange","OrangeBg","Track","Success"};
      string[] colors=light?new[]{"#FFFFFF","#252525","#666666","#F0F0F0","#E6E6E6","#235991","#E8F0FC","#915817","#FFF0DC","#E4E4E4","#287B51"}:new[]{"#2C2C2C","#ECECEC","#A6A6A6","#3A3A3A","#414141","#9CC4FF","#283950","#E9AF72","#4D3B2A","#515151","#7BC99A"};
      for(int i=0;i<keys.Length;i++){var old=window.Resources[keys[i]] as SolidColorBrush;var to=(Color)ColorConverter.ConvertFromString(colors[i]);var brush=new SolidColorBrush(to);if(animate&&SystemParameters.ClientAreaAnimation)brush.BeginAnimation(SolidColorBrush.ColorProperty,new ColorAnimation(old.Color,to,TimeSpan.FromMilliseconds(180)));window.Resources[keys[i]]=brush;}
      Find<Button>("DarkButton").FontWeight=light?FontWeights.Normal:FontWeights.SemiBold;Find<Button>("LightButton").FontWeight=light?FontWeights.SemiBold:FontWeights.Normal;
      Find<Button>("DarkButton").Background=light?Brushes.Transparent:(Brush)window.Resources["Hover"];Find<Button>("LightButton").Background=light?(Brush)window.Resources["Hover"]:Brushes.Transparent;
      Find<Button>("ChineseButton").Background=L.English?Brushes.Transparent:(Brush)window.Resources["Hover"];Find<Button>("EnglishButton").Background=L.English?(Brush)window.Resources["Hover"]:Brushes.Transparent;
      orb=orb??Find<QuotaOrb>("Orb");orb.InvalidateVisual();
    }
    void StartCollector(){
      try{var psi=new ProcessStartInfo{FileName="pythonw.exe",Arguments="-X utf8 \""+Path.Combine(root,"runtime","collector.py")+"\" --data-dir \""+dataDir+"\" --parent "+Process.GetCurrentProcess().Id,UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root};collector=Process.Start(psi);}
      catch{Find<TextBlock>("EmptyHint").Text=L.Get("PythonRequired");}
    }
    void ThreadClick(object sender,RoutedEventArgs e){var button=e.OriginalSource as Button;if(button==null)button=e.Source as Button;if(button==null)return;string id=button.Tag as string;if(id==null || !Regex.IsMatch(id,"^[A-Za-z0-9_-]{1,100}$"))return;try{Process.Start(new ProcessStartInfo("codex://threads/"+Uri.EscapeDataString(id)){UseShellExecute=true});}catch{button.ToolTip=L.Get("OpenFailed");}e.Handled=true;}
    void Tick(object sender,EventArgs e){
      try{if(!demo){string file=Path.Combine(dataDir,"snapshot.json");var at=File.GetLastWriteTimeUtc(file);if(at>lastSnapshot){var snapshot=Data.Read(file);if(snapshot.Count>0){lastSnapshot=at;ApplySnapshot(snapshot);}}}}
      catch(IOException){}
      bool changed=false;foreach(var row in rows.ToArray()){if(row.RemovalAt>0 && Data.Now>=row.RemovalAt){rows.Remove(row);changed=true;}}if(changed)UpdateVisible();
      if(quotaPopup.IsOpen)UpdateQuotaDetails();
    }
    void ApplySnapshot(Dictionary<string,object> snapshot){
      var incoming=new HashSet<string>();
      foreach(var raw in Data.Arr(snapshot,"threads")){
        var data=Data.Obj(raw);string id=Data.Str(data,"id"),turn=Data.Str(data,"turnId"),state=Data.Str(data,"state","working"),token=id+":"+turn;
        if(!Regex.IsMatch(id,"^[A-Za-z0-9_-]{1,100}$"))continue;incoming.Add(id);var row=rows.Find(r=>r.Id==id);
        if(row==null){if(state=="interrupted"||done.Contains(token))continue;row=new ThreadRow{Id=id,TurnId=turn};rows.Add(row);}
        if(row.TurnId!=turn){rows.Remove(row);row=new ThreadRow{Id=id,TurnId=turn};rows.Add(row);}
        row.Title=Data.Str(data,"title","未命名对话");row.Detail=Data.Str(data,"detail");
        if(state=="completed"&&row.State!="completed"){row.RemovalAt=collapsed?0:Data.Now+1.3;if(collapsed)pendingCompletion=true;done.Add(token);}else if(state=="interrupted")row.RemovalAt=Data.Now+.25;
        row.State=state;
      }
      foreach(var row in rows.ToArray())if(!incoming.Contains(row.Id)&&row.RemovalAt==0&&row.State!="completed"){rows.Remove(row);}
      quota=Data.Obj(Data.Get(snapshot,"quota"));if(refreshing && (Data.Num(quota,"attemptAt")>=refreshStarted || Data.Now-refreshStarted>40)){refreshing=false;Find<Button>("RefreshButton").IsEnabled=true;Find<Button>("RefreshButton").Content=L.Get("Refresh");}UpdateQuotaSummary();
      statusError=Data.Str(snapshot,"statusError");Find<TextBlock>("EmptyHint").Text=L.Get(statusError==""?"EmptyHint":"StatusUnavailable");
      UpdateVisible();
    }
    void UpdateVisible(){
      int active=rows.Count(r=>r.State!="completed"&&r.State!="interrupted");Find<TextBlock>("CountText").Text=active.ToString();
      var visible=rows.ToList();Find<ScrollViewer>("ThreadScroll").MaxHeight=maxVisible*42;
      for(int i=displayed.Count-1;i>=0;i--)if(!visible.Contains(displayed[i]))displayed.RemoveAt(i);
      for(int i=0;i<visible.Count;i++){int existing=displayed.IndexOf(visible[i]);if(existing<0)displayed.Insert(Math.Min(i,displayed.Count),visible[i]);else if(existing!=i)displayed.Move(existing,i);}
      Find<StackPanel>("EmptyState").Visibility=rows.Count==0?Visibility.Visible:Visibility.Collapsed;
      UpdateBadge();
    }
    static string ResetText(double at){double seconds=at-Data.Now;if(at<=0)return "";if(seconds<=0)return L.English?"Resetting soon":"即将重置";var span=TimeSpan.FromSeconds(seconds);if(L.English)return "Resets in "+(span.TotalDays>=1?((int)span.TotalDays)+"d "+span.Hours+"h":span.TotalHours>=1?((int)span.TotalHours)+"h "+span.Minutes+"m":Math.Max(1,(int)Math.Ceiling(span.TotalMinutes))+"m");string duration=span.TotalDays>=1?((int)span.TotalDays)+" 天 "+span.Hours+" 小时":span.TotalHours>=1?((int)span.TotalHours)+" 小时 "+span.Minutes+" 分钟":Math.Max(1,(int)Math.Ceiling(span.TotalMinutes))+" 分钟";return duration+"后重置";}
    TextBlock Text(string value,int size,string brush){return new TextBlock{Text=value,FontSize=size,Foreground=(Brush)window.Resources[brush]};}
    void UpdateQuotaDetails(){
      var host=Find<StackPanel>("QuotaDetails");host.Children.Clear();
      var windows=Data.Arr(quota,"windows").Select(Data.Obj).OrderBy(q=>Data.Num(q,"minutes",double.MaxValue)).ToList();
      foreach(var item in windows){
        var group=new StackPanel{Margin=new Thickness(0,0,0,17)};var line=new Grid();line.ColumnDefinitions.Add(new ColumnDefinition());line.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        line.Children.Add(Text(WindowLabel(item,true),12,"Text"));double remaining=Data.Num(item,"remaining");var amount=Text(remaining.ToString("0.#",CultureInfo.InvariantCulture)+(L.English?"% left":"% 剩余"),12,"Text");amount.FontWeight=FontWeights.SemiBold;Grid.SetColumn(amount,1);line.Children.Add(amount);group.Children.Add(line);
        var track=new Border{Height=4,Background=(Brush)window.Resources["Track"],CornerRadius=new CornerRadius(2),Margin=new Thickness(0,8,0,6),ClipToBounds=true};
        var bar=new Grid();bar.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(Math.Max(0,remaining),GridUnitType.Star)});bar.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(Math.Max(0,100-remaining),GridUnitType.Star)});bar.Children.Add(new Border{Background=new SolidColorBrush(QuotaOrb.ColorFor(remaining,Equals(window.Resources["Theme"],"light"))),CornerRadius=new CornerRadius(2)});track.Child=bar;group.Children.Add(track);
        var reset=Text(ResetText(Data.Num(item,"resetsAt")),11,"Muted");group.Children.Add(reset);host.Children.Add(group);
      }
      var purchased=Data.Obj(Data.Get(quota,"purchased"));if(purchased.Count>0){var line=new Grid{Margin=new Thickness(0,0,0,12)};line.Children.Add(Text(L.English?"Credits":"额度",12,"Text"));var amount=Text(Data.Str(purchased,"balance"),12,"Text");amount.HorizontalAlignment=HorizontalAlignment.Right;amount.FontWeight=FontWeights.SemiBold;line.Children.Add(amount);host.Children.Add(line);}
      string message=QuotaMessage();if(windows.Count>0&&Data.Num(quota,"checkedAt")>0)message=(L.English?"Updated ":"更新于 ")+new DateTime(1970,1,1,0,0,0,DateTimeKind.Utc).AddSeconds(Data.Num(quota,"checkedAt")).ToLocalTime().ToString("HH:mm");Find<TextBlock>("QuotaMessage").Text=refreshing?L.Get("Refreshing"):message;
    }
    void LoadDemo(){
      var samples=L.English?new[]{new[]{"Build the desktop widget","working"},new[]{"Review the release settings","question"},new[]{"Restore the server connection","reconnecting"},new[]{"Sync the project files","offline"},new[]{"Update the documentation","working"},new[]{"Check the latest changes","working"}}:new[]{new[]{"正在开发 Codex 悬浮窗","working"},new[]{"需要确认配置的对话","question"},new[]{"修复界面交互与过渡动画","reconnecting"},new[]{"网络连接暂时中断","offline"},new[]{"第五个对话 · 滚动查看","working"},new[]{"第六个对话 · 滚动查看","working"}};
      foreach(var sample in samples)rows.Add(new ThreadRow{Id=Guid.NewGuid().ToString(),TurnId="preview",Title=sample[0],State=sample[1],Detail="界面预览数据"});
      quota=Data.Obj(Data.Json.DeserializeObject("{\"ok\":true,\"checkedAt\":"+Data.Now.ToString(CultureInfo.InvariantCulture)+",\"windows\":[{\"label\":\"5 小时额度\",\"minutes\":300,\"remaining\":76,\"resetsAt\":"+(Data.Now+8220).ToString(CultureInfo.InvariantCulture)+"},{\"label\":\"每周额度\",\"minutes\":10080,\"remaining\":43,\"resetsAt\":"+(Data.Now+183600).ToString(CultureInfo.InvariantCulture)+"}],\"purchased\":{\"balance\":\"25\"}}"));UpdateQuotaSummary();UpdateVisible();
    }
    public void Show(){window.Show();}
  }

  public static class Program {
    [STAThread]public static void Main(string[] args){
      bool demo=args.Contains("--demo");bool created;using(var mutex=new Mutex(true,"Local\\CodexFloatWidget"+(demo?"Preview":""),out created)){
        if(!created)return;
        string root=Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).FullName;
        try{var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};var controller=new Controller(root,demo);controller.Show();app.Run();}
        catch(Exception error){try{Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexFloat"));File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexFloat","ui-error.log"),error.ToString());}catch{}MessageBox.Show(L.Get("StartupError"),"Codex Float");}
      }
    }
  }
}
