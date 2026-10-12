using System;
using System.Collections.Generic;
using System.Linq;
using Baiye.CryogenicPantry.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace Baiye.CryogenicPantry
{
    // Canonical native palette. DESIGN.md mirrors these accepted runtime tokens.
    public static class PantryPalette
    {
        public static readonly Color Ink=Hex("172E45"),Surface=Hex("F2F7FB"),Panel=Hex("E3EDF5"),Accent=Hex("75E7ED"),Muted=Hex("3D586C"),Warning=Hex("AB530C");
        static Color Hex(string h)=>new Color(Convert.ToByte(h.Substring(0,2),16)/255f,Convert.ToByte(h.Substring(2,2),16)/255f,Convert.ToByte(h.Substring(4,2),16)/255f,1);
    }
    public sealed class PantrySideScreen : SideScreenContent
    {
        public LayoutElement ButtonTemplate;
        public LayoutElement HorizontalButtonTemplate;
        public LocText LabelTemplate;
        PantryBuilding target;
        bool built,resetScroll,gasChooserOpen;
        int page;
        float nextRefresh;
        string expanded;
        readonly Dictionary<KButton,LocText> captions=new Dictionary<KButton,LocText>();
        readonly Dictionary<KButton,GameObject> buttonRoots=new Dictionary<KButton,GameObject>();
        readonly List<ScriptableObject> styles=new List<ScriptableObject>();
        readonly GameObject[] pages=new GameObject[3];
        readonly KButton[] tabs=new KButton[3];
        readonly Vector3[] corners=new Vector3[4];
        readonly Dictionary<string,KButton> recipeButtons=new Dictionary<string,KButton>();
        readonly Dictionary<string,LocText> recipeStatuses=new Dictionary<string,LocText>();
        readonly Dictionary<string,GameObject> recipeDetails=new Dictionary<string,GameObject>();
        readonly Dictionary<string,KButton> enables=new Dictionary<string,KButton>(),infinite=new Dictionary<string,KButton>();
        readonly Dictionary<string,LocText> targetNumbers=new Dictionary<string,LocText>();
        readonly Dictionary<string,LocText> reserves=new Dictionary<string,LocText>();
        readonly Dictionary<SimHashes,KButton> gasChoices=new Dictionary<SimHashes,KButton>();
        LocText status,temperature,foodTemperature,gas,gasBar,gasSupply,capacity,activity,accounts,productionSummary;
        KButton gasToggle,drinkToggle,gasChoice,cancelGas;
        GameObject gasChooser;
        RectTransform gasFill;
        LayoutElement size,scrollSize;
        RectTransform contentRect;
        ScrollRect scroll;
        public override bool IsValidForTarget(GameObject go)=>go!=null&&go.GetComponent<PantryBuilding>()?.Ready==true;
        public override int GetSideScreenSortOrder()=>80;
        public override string GetTitle()=>PantryText.T("气调与饮料","Atmosphere & Beverages");
        public override void SetTarget(GameObject go){target=go?.GetComponent<PantryBuilding>();expanded=null;gasChooserOpen=false;page=0;resetScroll=true;if(target==null||!target.Ready){target=null;return;}Build();Refresh();}
        public override void ClearTarget(){target=null;}
        protected override void OnCleanUp(){target=null;foreach(var s in styles)if(s!=null)Destroy(s);styles.Clear();base.OnCleanUp();}
        void Update(){if(target!=null&&target.Ready&&isActiveAndEnabled&&Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.5f;Refresh();}}
        void LateUpdate()
        {
            if(!built||target==null||!target.Ready||!isActiveAndEnabled)return;
            ResizePanel();
            if(resetScroll){scroll.StopMovement();contentRect.anchoredPosition=Vector2.zero;scroll.verticalNormalizedPosition=1;resetScroll=false;}
        }
        static void Layout(VerticalLayoutGroup l,float gap){l.spacing=gap;l.childControlWidth=true;l.childControlHeight=true;l.childForceExpandWidth=true;l.childForceExpandHeight=false;}
        GameObject Group(GameObject parent,string name,Color? background=null)
        {
            var g=new GameObject(name,typeof(RectTransform),typeof(VerticalLayoutGroup));g.layer=gameObject.layer;g.transform.SetParent(parent.transform,false);var l=g.GetComponent<VerticalLayoutGroup>();Layout(l,5);
            if(background.HasValue){g.AddComponent<Image>().color=background.Value;l.padding=new RectOffset(10,10,8,8);}return g;
        }
        GameObject Row(GameObject parent)
        {
            var g=new GameObject("Controls",typeof(RectTransform),typeof(HorizontalLayoutGroup));g.layer=gameObject.layer;g.transform.SetParent(parent.transform,false);
            var l=g.GetComponent<HorizontalLayoutGroup>();l.spacing=6;l.childControlHeight=true;l.childControlWidth=true;l.childForceExpandWidth=true;l.childForceExpandHeight=false;return g;
        }
        LocText Label(GameObject parent,float height=24,float font=15,Color? color=null)
        {
            var g=Util.KInstantiateUI(LabelTemplate.gameObject,parent,false);var t=g.GetComponent<LocText>();StyleText(t,font,color??PantryPalette.Ink);
            var l=g.AddOrGet<LayoutElement>();l.minHeight=height;l.preferredHeight=-1;l.minWidth=0;l.preferredWidth=-1;l.flexibleWidth=1;l.flexibleHeight=0;l.ignoreLayout=false;g.SetActive(true);return t;
        }
        void StyleText(LocText text,float font,Color color)
        {
            // LocText.Start/RefreshText can reapply the template's white style.
            // Own both references so late initialization uses the same dark text.
            var owned=text.textStyleSetting!=null?Instantiate(text.textStyleSetting):ScriptableObject.CreateInstance<TextStyleSetting>();
            owned.sdfFont=text.font;owned.fontSize=Mathf.RoundToInt(font);owned.textColor=color;owned.enableWordWrapping=true;
            styles.Add(owned);text.key="";text.allowOverride=false;text.textStyleSetting=owned;
            text.gameObject.AddOrGet<SetTextStyleSetting>().SetStyle(owned);
            text.fontSize=font;text.color=color;text.raycastTarget=false;text.textWrappingMode=TMPro.TextWrappingModes.Normal;
        }
        KButton Button(GameObject parent,string text,System.Action action,float font=15)
        {
            bool horizontal=parent.GetComponent<HorizontalLayoutGroup>()!=null;
            var template=horizontal&&HorizontalButtonTemplate!=null?HorizontalButtonTemplate:ButtonTemplate;
            var g=Util.KInstantiateUI(template.gameObject,parent,false);var b=g.GetComponentInChildren<KButton>(true);var label=g.GetComponentInChildren<LocText>(true);
            var layout=g.AddOrGet<LayoutElement>();layout.minHeight=layout.preferredHeight=34;layout.minWidth=0;layout.preferredWidth=0;layout.flexibleWidth=1;layout.flexibleHeight=0;layout.ignoreLayout=false;layout.layoutPriority=10;
            StyleText(label,font,PantryPalette.Ink);captions[b]=label;buttonRoots[b]=g;
            if(horizontal){label.textStyleSetting.enableWordWrapping=false;label.textWrappingMode=TMPro.TextWrappingModes.NoWrap;}
            foreach(var image in g.GetComponentsInChildren<KImage>(true))
            {
                // Each clone owns its style. Never modify the shared vanilla asset.
                var style=ScriptableObject.CreateInstance<ColorStyleSetting>();style.activeColor=PantryPalette.Accent;style.inactiveColor=PantryPalette.Panel;
                style.hoverColor=Color.Lerp(PantryPalette.Panel,PantryPalette.Accent,.35f);style.disabledColor=PantryPalette.Surface;style.disabledActiveColor=PantryPalette.Surface;style.disabledhoverColor=PantryPalette.Surface;
                styles.Add(style);image.colorStyleSetting=style;image.ApplyColorStyleSetting();
            }
            b.ClearOnClick();b.onClick+=()=>{if(!built||target==null||!target.Ready||!g.activeInHierarchy||!b.isInteractable)return;action();Refresh();};Caption(b,text,true);g.SetActive(true);return b;
        }
        void Caption(KButton b,string text,bool enabled){captions[b].SetText(text);b.isInteractable=enabled;b.GetComponentInChildren<ToolTip>(true)?.SetSimpleTooltip(text);}
        void Highlight(KButton b,bool selected)
        {foreach(var image in buttonRoots[b].GetComponentsInChildren<KImage>(true))if(image.colorStyleSetting!=null){image.colorStyleSetting.inactiveColor=selected?PantryPalette.Accent:PantryPalette.Panel;image.ApplyColorStyleSetting();}}
        RectTransform Meter(GameObject parent)
        {
            var track=new GameObject("GasCapacityMeter",typeof(RectTransform),typeof(Image),typeof(LayoutElement));track.layer=gameObject.layer;track.transform.SetParent(parent.transform,false);
            track.GetComponent<Image>().color=PantryPalette.Muted;track.GetComponent<Image>().raycastTarget=false;
            var layout=track.GetComponent<LayoutElement>();layout.minHeight=layout.preferredHeight=6;layout.flexibleHeight=0;
            var fill=new GameObject("Fill",typeof(RectTransform),typeof(Image));fill.layer=gameObject.layer;fill.transform.SetParent(track.transform,false);
            fill.GetComponent<Image>().color=PantryPalette.Accent;fill.GetComponent<Image>().raycastTarget=false;
            var rect=fill.GetComponent<RectTransform>();Stretch(rect);return rect;
        }
        void Build()
        {
            if(built)return;
            size=gameObject.AddOrGet<LayoutElement>();size.minWidth=0;size.preferredWidth=300;size.minHeight=0;size.flexibleHeight=0;
            var root=gameObject.AddOrGet<VerticalLayoutGroup>();Layout(root,PantryPanelLayout.Gap);
            var navigation=Row(gameObject);var nl=navigation.AddComponent<LayoutElement>();nl.minHeight=nl.preferredHeight=PantryPanelLayout.NavigationHeight;nl.flexibleHeight=0;
            for(int n=0;n<3;n++){int selected=n;tabs[n]=Button(navigation,"",()=>{if(page!=selected){page=selected;resetScroll=true;}});}
            var host=new GameObject("PantryScrollHost",typeof(RectTransform),typeof(Image),typeof(ScrollRect),typeof(LayoutElement));host.layer=gameObject.layer;host.transform.SetParent(transform,false);host.GetComponent<Image>().color=PantryPalette.Surface;
            scrollSize=host.GetComponent<LayoutElement>();scrollSize.minHeight=0;scrollSize.preferredHeight=240;scrollSize.flexibleHeight=0;
            var viewport=new GameObject("PantryViewport",typeof(RectTransform),typeof(Image),typeof(RectMask2D));viewport.layer=gameObject.layer;viewport.transform.SetParent(host.transform,false);viewport.GetComponent<Image>().color=PantryPalette.Surface;
            var vr=viewport.GetComponent<RectTransform>();Stretch(vr);vr.offsetMax=new Vector2(-16,0);
            var content=Group(viewport,"PantryContent");contentRect=content.GetComponent<RectTransform>();contentRect.anchorMin=new Vector2(0,1);contentRect.anchorMax=Vector2.one;contentRect.pivot=new Vector2(.5f,1);contentRect.sizeDelta=Vector2.zero;
            content.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll=host.GetComponent<ScrollRect>();scroll.viewport=vr;scroll.content=contentRect;scroll.horizontal=false;scroll.vertical=true;scroll.inertia=false;scroll.scrollSensitivity=24;scroll.movementType=ScrollRect.MovementType.Clamped;
            scroll.verticalScrollbar=BuildScrollbar(host);scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.Permanent;
            pages[0]=Group(content,"PreservationPage",PantryPalette.Surface);
            var header=Group(pages[0],"Preservation",PantryPalette.Ink);
            status=Label(header,36,17,Color.white);temperature=Label(header,30,23,Color.white);foodTemperature=Label(header,22,13,Color.white);
            gas=Label(header,42,14,Color.white);gasFill=Meter(header);gasBar=Label(header,20,12,PantryPalette.Accent);
            capacity=Label(header,38,14,Color.white);activity=Label(header,38,13,PantryPalette.Accent);
            gasToggle=Button(pages[0],"",()=>target.AutoGas=!target.AutoGas);
            gasChoice=Button(pages[0],"",()=>gasChooserOpen=!gasChooserOpen);
            gasChooser=Group(pages[0],"GasChooser",PantryPalette.Panel);
            Label(gasChooser,24,13).SetText(PantryText.T("选择目标气体；饮料加工需要二氧化碳。","Select target gas; drinks require carbon dioxide."));
            int GasOrder(Element e)=>e.id==SimHashes.CarbonDioxide?0:e.id==SimHashes.ChlorineGas?1:e.id==SimHashes.Hydrogen?2:3;
            foreach(var e in ElementLoader.elements.Where(PantryBuilding.SupportsGas).OrderBy(GasOrder).ThenBy(e=>(int)e.id))
            {var id=e.id;gasChoices[id]=Button(gasChooser,PantryBuilding.GasName(id),()=>{if(target.SelectGas(id))gasChooserOpen=false;});}
            cancelGas=Button(pages[0],"",()=>{target.CancelGasReplacement();gasChooserOpen=false;});
            gasSupply=Label(pages[0],40,13,PantryPalette.Muted);
            Label(pages[0],48,13).SetText(PantryText.T("接入所选气体，温度不得超过50°C。换气消耗实际来气；错误或过热气体留在管内。无菌气氛不消除病菌。","Supply selected gas at 50 C or less. Replacement uses real incoming gas; wrong or hot gas stays in the pipe. Sterility does not remove germs."));
            var production=pages[1]=Group(content,"ProductionPage",PantryPalette.Surface);
            drinkToggle=Button(production,"",()=>target.AutoDrinks=!target.AutoDrinks);
            productionSummary=Label(production,36,13,PantryPalette.Muted);
            foreach(var r in DrinkRecipes.All)
            {
                string id=r.Id;var card=Group(production,id,PantryPalette.Panel);
                var cardLayout=card.GetComponent<VerticalLayoutGroup>();cardLayout.spacing=3;cardLayout.padding=new RectOffset(8,8,6,6);
                recipeButtons[id]=Button(card,"",()=>expanded=expanded==id?null:id,13);recipeStatuses[id]=Label(card,21,13,PantryPalette.Muted);
                var body=Group(card,"RecipeDetails");recipeDetails[id]=body;
                enables[id]=Button(body,"",()=>{int i=target.RecipeIds.IndexOf(id);target.Enabled[i]=!target.Enabled[i];});
                Label(body).SetText(PantryText.T("目标库存（份）","Target stock (servings)"));var controls=Row(body);
                Button(controls,PantryText.T("减少","Less"),()=>ChangeTarget(id,-1));targetNumbers[id]=Label(controls,34,19);Button(controls,PantryText.T("增加","More"),()=>ChangeTarget(id,1));
                infinite[id]=Button(body,"",()=>{int i=target.RecipeIds.IndexOf(id);target.Unlimited[i]=!target.Unlimited[i];});
                string ingredients=$"{PantryText.T("水","Water")} {r.Water:0.##} kg  ·  CO2 {r.CarbonDioxide:0.##} kg";
                foreach(var s in r.Solids)ingredients+=$"\n{PantryBuilding.Proper(s.Id)} {s.Mass:0.##} kg";
                Label(body,24+22*r.Solids.Length,13).SetText(ingredients);
                Label(body,70,13).SetText(EffectDescription(r)+$"\n{PantryText.T("加工","Processing")} {r.Seconds:0} s · {r.Mass:0.##} kg/{PantryText.T("份","serving")}");
                body.SetActive(false);
            }
            var settings=pages[2]=Group(content,"AdvancedPage",PantryPalette.Surface);
            Label(settings,24,16).SetText(PantryText.T("加工原料保留量","Ingredient reserves"));
            foreach(var id in new[]{"PrickleFruit","SpiceNut"})
            {
                string captured=id;Label(settings).SetText(PantryBuilding.Proper(id)+PantryText.T(" · 最低保留 kg"," · reserve kg"));var row=Row(settings);
                Button(row,PantryText.T("减少","Less"),()=>target.SetReserve(captured,Math.Max(0,target.Reserve(captured)-1)));reserves[id]=Label(row,34,19);Button(row,PantryText.T("增加","More"),()=>target.SetReserve(captured,target.Reserve(captured)+1));
            }
            accounts=Label(settings,120,13,PantryPalette.Muted);pages[1].SetActive(false);pages[2].SetActive(false);built=true;
        }
        static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}
        Scrollbar BuildScrollbar(GameObject parent)
        {
            var track=new GameObject("PantryScrollbar",typeof(RectTransform),typeof(Image),typeof(Scrollbar));track.layer=gameObject.layer;track.transform.SetParent(parent.transform,false);track.GetComponent<Image>().color=PantryPalette.Panel;
            var tr=track.GetComponent<RectTransform>();tr.anchorMin=new Vector2(1,0);tr.anchorMax=Vector2.one;tr.offsetMin=new Vector2(-12,0);tr.offsetMax=Vector2.zero;
            var area=new GameObject("SlidingArea",typeof(RectTransform));area.layer=gameObject.layer;area.transform.SetParent(track.transform,false);var ar=area.GetComponent<RectTransform>();Stretch(ar);ar.offsetMin=new Vector2(2,2);ar.offsetMax=new Vector2(-2,-2);
            var handle=new GameObject("Handle",typeof(RectTransform),typeof(Image));handle.layer=gameObject.layer;handle.transform.SetParent(area.transform,false);var hr=handle.GetComponent<RectTransform>();Stretch(hr);
            var bar=track.GetComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;bar.handleRect=hr;bar.targetGraphic=handle.GetComponent<Image>();bar.value=1;
            var colors=bar.colors;colors.normalColor=colors.disabledColor=PantryPalette.Muted;colors.highlightedColor=colors.pressedColor=colors.selectedColor=PantryPalette.Ink;bar.colors=colors;return bar;
        }
        void ResizePanel()
        {
            var canvas=GetComponentInParent<Canvas>();var scaler=GetComponentInParent<KCanvasScaler>();
            if(scaler==null&&GameScreenManager.Instance!=null&&GameScreenManager.Instance.ssOverlayCanvas!=null)scaler=GameScreenManager.Instance.ssOverlayCanvas.GetComponent<KCanvasScaler>();
            float scale=Mathf.Max(.01f,scaler!=null?scaler.GetCanvasScale():canvas!=null?canvas.scaleFactor:1);
            Camera camera=canvas!=null&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            var rect=GetComponent<RectTransform>();rect.GetWorldCorners(corners);float top=RectTransformUtility.WorldToScreenPoint(camera,corners[1]).y;
            // Before the first layout pass use a conservative budget; LateUpdate
            // replaces it with the actual panel position, rather than full-screen height.
            if(top<=0||top>Screen.height)top=Mathf.Max(0,Screen.height-180*scale);
            float bottom=18*scale;
            foreach(var mask in GetComponentsInParent<RectMask2D>())if(mask.isActiveAndEnabled)
            {
                mask.rectTransform.GetWorldCorners(corners);bottom=Mathf.Max(bottom,RectTransformUtility.WorldToScreenPoint(camera,corners[0]).y);top=Mathf.Min(top,RectTransformUtility.WorldToScreenPoint(camera,corners[1]).y);
            }
            float available=Mathf.Max(0,(top-bottom)/scale);
            var parent=transform.parent;var siblings=parent!=null?parent.GetComponent<VerticalLayoutGroup>():null;
            if(siblings!=null)
            {
                available-=siblings.padding.bottom;
                for(int n=transform.GetSiblingIndex()+1;n<parent.childCount;n++)
                {
                    var child=parent.GetChild(n);var layout=child.GetComponent<LayoutElement>();
                    if(!child.gameObject.activeInHierarchy||(layout!=null&&layout.ignoreLayout))continue;
                    var siblingRect=child as RectTransform;if(siblingRect!=null)available-=Mathf.Max(0,LayoutUtility.GetPreferredHeight(siblingRect))+siblings.spacing;
                }
            }
            float height=PantryPanelLayout.Height(available,LayoutUtility.GetPreferredHeight(contentRect));
            if(Mathf.Abs(size.preferredHeight-height)<.5f)return;
            size.preferredHeight=height;scrollSize.preferredHeight=Mathf.Max(0,height-PantryPanelLayout.ChromeHeight);LayoutRebuilder.MarkLayoutForRebuild(rect);
        }
        void ChangeTarget(string id,float step){int n=target.RecipeIds.IndexOf(id);target.Targets[n]=Math.Max(0,Math.Min(500,target.Targets[n]+step));}
        public static string EffectDescription(DrinkRecipe r)
        {
            switch(r.Id)
            {
                case "BaiyeChilledSoda":return PantryText.T("士气 +1 · 压力 −5%/周期\n持续 0.5 周期","Morale +1 · stress -5%/cycle\nLasts 0.5 cycles");
                case "BaiyeBerrySoda":return PantryText.T("士气 +3 · 压力 −10%/周期\n持续 0.75 周期","Morale +3 · stress -10%/cycle\nLasts 0.75 cycles");
                case "BaiyePepperSoda":return PantryText.T("运动 +2 · 操作 +2\n持续 0.5 周期","Athletics +2 · machinery +2\nLasts 0.5 cycles");
                case "BaiyeEnergySoda":return PantryText.T("操作/建造/挖掘/运动 +3，持续 0.3 周期\n随后操作/运动 −1，持续 0.15 周期","Machinery/construction/digging/athletics +3 for 0.3 cycles\nThen machinery/athletics -1 for 0.15 cycles");
                default:return PantryText.T("饮用后获得对应配方效果。","Applies the registered recipe effect.");
            }
        }
        void Refresh()
        {
            if(!built||target==null||!target.Ready)return;var b=target;
            string[] names={PantryText.T("保鲜","Preserve"),PantryText.T("饮料","Drinks"),PantryText.T("高级","Advanced")};
            for(int n=0;n<pages.Length;n++)
            {
                pages[n].SetActive(page==n);Caption(tabs[n],page==n?"<b>"+names[n]+"</b>":names[n],true);
                Highlight(tabs[n],page==n);
            }
            status.SetText(b.PreservationStatus);temperature.SetText($"{b.InternalK-273.15f:0.0}°C <size=13>{PantryText.T("目标 −22°C","target -22°C")}</size>");
            foodTemperature.SetText(b.WarmestFoodK.HasValue?$"{PantryText.T("最暖食品","Warmest food")} {b.WarmestFoodK.Value-273.15f:0.0}°C":PantryText.T("食品仓为空或没有易腐食品","No perishable food stored"));
            string name=PantryBuilding.GasName((SimHashes)b.SelectedGas);
            string stored=b.Gas.MassStored()>.0001f?PantryBuilding.GasName(b.StoredGas):PantryText.T("空罐","Empty");
            bool mixed=b.Gas.items.Where(g=>g!=null).Select(g=>g.GetComponent<PrimaryElement>().ElementID).Distinct().Count()>1;
            gas.SetText($"{PantryText.T("当前","Current")} {stored}{(mixed?PantryText.T("（混合）"," (mixed)"):"")} {b.Gas.MassStored():0.00}/2 kg\n{PantryText.T("目标","Target")} {name}");
            gasFill.anchorMax=new Vector2(Mathf.Clamp01(b.Gas.MassStored()/PantryRules.GasCapacity),1);
            gasBar.SetText(b.Replacing?$"{PantryText.T("目标气体比例","Target gas ratio")} {b.GasRatio:P0} · {PantryText.T("等待或正在置换","pending/replacing")}":PantryText.T("气体储量","Gas storage")+$" {b.Gas.MassStored()/PantryRules.GasCapacity:P0}");
            capacity.SetText($"{PantryText.T("食品","Food")}   {b.Food.MassStored():0.0} / 500 kg\n{PantryText.T("饮料","Drinks")}   {b.DrinkMass:0.0} / 50 kg");
            activity.SetText(b.StopReason!=""?PantryText.T("设备停机：","Stopped: ")+b.StopReason:$"{(b.Cooling?PantryText.T("主动降温","Cooling"):PantryText.T("恒温维护","Temperature control"))} · {b.LastWatts:0} W\n{PantryText.T("排热","Heat")} {b.LastHeatKW:0.0} kDTU/s");
            productionSummary.SetText($"{PantryText.T("饮料","Drinks")} {b.DrinkMass:0.0}/50 kg · {PantryText.T("水","Water")} {b.Water.MassStored():0.0}/20 kg");
            foreach(var r in DrinkRecipes.All)
            {
                int i=b.RecipeIds.IndexOf(r.Id);string stock=b.Unlimited[i]?PantryText.T("不限","unlimited"):b.Targets[i].ToString("0");
                Caption(recipeButtons[r.Id],$"{PantryText.T(r.Chinese,r.English)} {b.Servings(r):0}/{stock} · {(expanded==r.Id?PantryText.T("收起","Hide"):PantryText.T("展开","Show"))}",true);
                recipeStatuses[r.Id].SetText(b.RecipeStatus(r));recipeDetails[r.Id].SetActive(expanded==r.Id);
                Caption(enables[r.Id],b.Enabled[i]?PantryText.T("暂停此配方","Pause recipe"):PantryText.T("启用此配方","Enable recipe"),true);
                Caption(infinite[r.Id],b.Unlimited[i]?PantryText.T("无限生产：开","Unlimited: on"):PantryText.T("无限生产：关","Unlimited: off"),true);targetNumbers[r.Id].SetText(b.Targets[i].ToString("0"));
            }
            Caption(gasToggle,PantryText.T("自动补气：","Auto gas: ")+(b.AutoGas?PantryText.T("开","on"):PantryText.T("关","off")),true);
            Caption(drinkToggle,PantryText.T("自动加工：","Auto production: ")+(b.AutoDrinks?PantryText.T("开","on"):PantryText.T("关","off")),true);
            Caption(gasChoice,gasChooserOpen?PantryText.T("收起气体选择","Hide gas selection"):PantryText.T("选择保鲜气体","Select preservation gas"),true);
            gasChooser.SetActive(gasChooserOpen);
            foreach(var pair in gasChoices){Caption(pair.Value,PantryBuilding.GasName(pair.Key)+(b.SelectedGas==(int)pair.Key?PantryText.T(" · 已选"," · selected"):""),true);Highlight(pair.Value,b.SelectedGas==(int)pair.Key);}
            Caption(cancelGas,PantryText.T("取消更换：恢复旧气体目标","Cancel change: restore old gas target"),b.CanCancelGasReplacement);buttonRoots[cancelGas].SetActive(b.Replacing);
            gasSupply.SetText(b.GasSupplyStatus);
            foreach(var id in reserves.Keys)reserves[id].SetText(b.Reserve(id).ToString("0"));
            accounts.SetText($"{PantryText.T("水缓存","Water buffer")} {b.Water.MassStored():0.00}/20 kg\n{PantryText.T("配方 CO2","Recipe CO2")} {b.RecipeGasUsed:0.000} kg\n{PantryText.T("密封损耗","Seal loss")} {b.SealGasUsed:0.000} kg\n{PantryText.T("气氛置换消耗","Replacement loss")} {b.PurgeGasUsed:0.000} kg\n{PantryText.T("实际耗电","Paid energy")} {b.ElectricalJoules/1000:0.0} kJ");
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);ResizePanel();
        }
    }
    [HarmonyPatch(typeof(DetailsScreen),"OnPrefabInit")]
    internal static class PantryScreenRegistration
    {
        static void Postfix(DetailsScreen __instance)
        {
            var list=(List<DetailsScreen.SideScreenRef>)AccessTools.Field(typeof(DetailsScreen),"sideScreens").GetValue(__instance);
            if(list.Any(s=>s.screenPrefab is PantrySideScreen))return;
            var template=list.Select(s=>s.screenPrefab).OfType<ButtonMenuSideScreen>().FirstOrDefault();var tab=__instance.GetTabOfType(DetailsScreen.SidescreenTabTypes.Config);
            if(template==null||tab?.bodyInstance==null){Debug.LogWarning("[BaiyeCryogenicPantry] Native UI templates unavailable");return;}
            var go=new GameObject("BaiyePantrySettings",typeof(RectTransform),typeof(LayoutElement));go.SetActive(false);go.layer=template.gameObject.layer;go.transform.SetParent(tab.bodyInstance.transform,false);
            var screen=go.AddComponent<PantrySideScreen>();screen.ContentContainer=go;screen.ButtonTemplate=template.buttonPrefab;screen.HorizontalButtonTemplate=template.horizontalButtonPrefab;screen.LabelTemplate=template.buttonPrefab.GetComponentInChildren<LocText>(true);
            list.Add(new DetailsScreen.SideScreenRef{name="BaiyePantrySettings",screenPrefab=screen,screenInstance=screen,tab=DetailsScreen.SidescreenTabTypes.Config});
        }
    }
}
