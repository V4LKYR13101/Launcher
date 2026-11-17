package com.launcher.overlayinspector;
import android.app.*;import android.os.*;import android.accessibilityservice.*;import android.view.accessibility.*;import android.graphics.Rect;import org.xmlpull.v1.XmlSerializer;import android.util.Xml;import java.io.StringWriter;
public class Inspector extends Instrumentation {
 public void onCreate(Bundle b){super.onCreate(b);start();}
 public void onStart(){Bundle result=new Bundle();try{
  UiAutomation automation=getUiAutomation(UiAutomation.FLAG_DONT_SUPPRESS_ACCESSIBILITY_SERVICES);
  AccessibilityServiceInfo info=automation.getServiceInfo();info.flags|=AccessibilityServiceInfo.FLAG_RETRIEVE_INTERACTIVE_WINDOWS;automation.setServiceInfo(info);
  StringWriter out=new StringWriter();XmlSerializer xml=Xml.newSerializer();xml.setOutput(out);xml.startTag(null,"hierarchy");
  for(AccessibilityWindowInfo window:automation.getWindows()){AccessibilityNodeInfo root=window.getRoot();if(root!=null)dump(xml,root);}
  xml.endTag(null,"hierarchy");xml.flush();result.putString("xml",out.toString());finish(Activity.RESULT_OK,result);
 }catch(Exception e){result.putString("error",e.toString());finish(Activity.RESULT_CANCELED,result);}}
 static void dump(XmlSerializer xml,AccessibilityNodeInfo n)throws Exception{Rect b=new Rect();n.getBoundsInScreen(b);xml.startTag(null,"node");xml.attribute(null,"text",n.getText()==null?"":n.getText().toString());xml.attribute(null,"content-desc",n.getContentDescription()==null?"":n.getContentDescription().toString());xml.attribute(null,"bounds","["+b.left+","+b.top+"]["+b.right+","+b.bottom+"]");xml.attribute(null,"visible",String.valueOf(n.isVisibleToUser()));xml.attribute(null,"checked",String.valueOf(n.isChecked()));xml.attribute(null,"clickable",String.valueOf(n.isClickable()));for(int i=0;i<n.getChildCount();i++){AccessibilityNodeInfo child=n.getChild(i);if(child!=null)dump(xml,child);}xml.endTag(null,"node");}
}