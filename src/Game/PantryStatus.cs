using System;
namespace Baiye.CryogenicPantry
{
    public sealed class PantryStatus : KMonoBehaviour
    {
        KSelectable selectable;PantryBuilding pantry;Guid handle;
        protected override void OnSpawn()
        {
            base.OnSpawn();selectable=GetComponent<KSelectable>();pantry=GetComponent<PantryBuilding>();
            var item=new StatusItem("BaiyePantryState","{STATE}","{DETAIL}","",StatusItem.IconType.Info,NotificationType.Neutral,false,OverlayModes.None.ID,showWorldIcon:false);
            item.resolveStringCallback=(text,data)=>text.Replace("{STATE}",pantry.Ready?pantry.PreservationStatus:PantryText.T("保鲜柜初始化中","Initializing pantry")).Replace("{DETAIL}",Details());
            handle=selectable.AddStatusItem(item,this);
        }
        string Details()=>pantry.Ready?$"{pantry.InternalK-273.15f:0.0}°C · {pantry.Gas.MassStored():0.00}/2 kg\n"+
            PantryText.T("食品","Food")+$" {pantry.Food.MassStored():0.0}/500 kg · "+PantryText.T("饮料","Drinks")+$" {pantry.DrinkMass:0.0}/50 kg\n"+
            (pantry.WarmestFoodK.HasValue?PantryText.T("最暖食品","Warmest food")+$" {pantry.WarmestFoodK.Value-273.15f:0.0}°C\n":"")+
            (pantry.StopReason!=""?PantryText.T("停机原因：","Stopped: ")+pantry.StopReason:PantryText.T("设备已供电；保鲜取决于食品实际温度与有效气氛。","Powered; preservation depends on actual food temperature and a valid atmosphere."))+"\n"+
            pantry.GasSupplyStatus+"\n"+
            PantryText.T("打开“气调与饮料”设置生产、保留量和气氛。无菌气氛不消除病菌。","Open Atmosphere & Beverages to control recipes, reserves and gas. Sterility does not remove germs."):"";
        protected override void OnCleanUp(){if(selectable!=null&&handle!=Guid.Empty)selectable.RemoveStatusItem(handle);base.OnCleanUp();}
    }
}
