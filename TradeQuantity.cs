using System;
using System.Drawing;
using System.Windows.Forms;

namespace MountainTide
{
    internal sealed partial class Game
    {
        const int TradeSliderLeft=449, TradeSliderRight=831, TradeSliderY=397;
        int tradeItem=-1, tradeQuantity=1;
        bool tradeSliderHeld;
        string tradeNotice="";
        double tradeNoticeUntil=-1;
        string TradeCatalogNotice {get{return T<tradeNoticeUntil?tradeNotice:"";}}
        bool BuyingHere {get{return panel=="supply";}}
        bool SellingAtHarbor {get{return panel=="harbor";}}
        string TradeCurrency {get{return SellingAtHarbor?"星元":"现钱";}}
        string TradeUnit {get{return BuyingHere?"袋":"份";}}
        int CurrentTradeLimit {get{return state.TradeLimit(BuyingHere,SellingAtHarbor,tradeItem);}}
        string TradeItemName {
            get{return tradeItem==FarmState.FertilizerItem?"化肥":Crop.All[tradeItem].Name+(BuyingHere?"种子":"");}
        }

        void DrawTradeQuantity(Graphics g)
        {
            bool buy=BuyingHere, fertilizer=tradeItem==FarmState.FertilizerItem;
            int limit=CurrentTradeLimit;
            bool available=limit>0&&tradeQuantity>=1&&tradeQuantity<=limit;
            int price=FarmState.TradePrice(buy,SellingAtHarbor,tradeItem);
            int held=state.TradeHeld(buy,tradeItem);
            int balance=SellingAtHarbor?state.StarCredits:state.Cash;
            long total=(long)tradeQuantity*price;
            g.FillRectangle(B(Color.FromArgb(127,24,33,26)),0,0,W,H);
            Plate(g,359,153,568,406,Color.FromArgb(150,46,41,28),Color.FromArgb(110,46,41,28),14);
            Plate(g,355,147,570,406,Col("fcf3db"),Col("8b7851"),13);
            TextAt(g,buy?"购买货品":"出售收成",26,ink,381,161,394,42,true);
            ModalButton(g,858,166,42,"×","trade-back",true,false);

            Plate(g,380,216,519,99,Col("f4e8c9"),Col("d4c297"),9);
            Image picture=fertilizer?art.Tools[2]:buy?art.SeedPackets[tradeItem]:art.Plants[tradeItem,2];
            FitSprite(g,picture,391,226,71,72);
            TextAt(g,TradeItemName,21,ink,477,224,244,34,true);
            TextAt(g,fertilizer?"每份可提前 20 秒成熟":"成熟时间  "+Crop.All[tradeItem].Seconds+" 秒",15,Col("596650"),477,264,206,26);
            TextAt(g,"单价  "+price+" "+TradeCurrency,16,ink,689,228,196,29,true);
            TextAt(g,"当前持有  "+held+" "+TradeUnit,15,Col("596650"),689,264,200,26);

            TextAt(g,buy?"购买数量":"出售数量",17,ink,383,328,132,34,true);
            TextAt(g,limit>0?tradeQuantity+" "+TradeUnit:"—",24,ink,547,323,188,43,true,true);
            TextAt(g,(buy?"可买 ":"可售 ")+limit+" "+TradeUnit,14,Col("657259"),754,328,141,34,false,true);
            ModalButton(g,380,379,45,"−","trade-less",limit>0&&tradeQuantity>1,false);
            ModalButton(g,854,379,45,"+","trade-more",limit>0&&tradeQuantity<limit,false);
            int thumb=TradeSliderLeft;
            if(limit>1)thumb+=(int)Math.Round((TradeSliderRight-TradeSliderLeft)*(Math.Min(tradeQuantity,limit)-1.0)/(limit-1));
            Plate(g,TradeSliderLeft-3,TradeSliderY-4,TradeSliderRight-TradeSliderLeft+6,8,Col("ded5bc"),Col("b4a784"),4);
            if(thumb>TradeSliderLeft)g.FillRectangle(B(Col("72905b")),TradeSliderLeft,TradeSliderY-3,thumb-TradeSliderLeft,6);
            g.FillEllipse(B(Col("9a8764")),thumb-10,TradeSliderY-8,20,20);
            g.FillEllipse(B(limit>1?Col("edbf6e"):Col("d2c8ae")),thumb-10,TradeSliderY-10,20,20);
            using(Pen outline=new Pen(Col("8a7046"),1.5f))g.DrawEllipse(outline,thumb-10,TradeSliderY-10,20,20);
            g.FillEllipse(B(Col("ffedb3")),thumb-4,TradeSliderY-6,6,5);
            HitBox(TradeSliderLeft-13,TradeSliderY-21,TradeSliderRight-TradeSliderLeft+26,42,"trade-slider",limit>1);
            if(limit>1){
                TextAt(g,"1",12,Col("70785e"),TradeSliderLeft-18,417,36,18,false,true);
                TextAt(g,limit.ToString(),12,Col("70785e"),TradeSliderRight-23,417,46,18,false,true);
            }
            string note="每笔最多 "+FarmState.MaxTradeQuantity+" "+TradeUnit;
            if(limit==0)note=buy?(balance<price?"现钱不足，暂时无法购买":"背包已满，暂时无法购买"):
                (held==0?"背包里还没有这种作物":"钱包余额已达上限");
            else if(tradeQuantity>limit)note="可交易数量已变化，请重新选择";
            TextAt(g,note,13,limit==0?Col("945846"):Col("747961"),380,424,519,25,false,true);

            g.FillRectangle(B(Col("dcceb1")),380,459,519,1);
            TextAt(g,(buy?"合计花费  ":"合计可得  ")+(available?total.ToString():"—")+" "+TradeCurrency,20,ink,383,470,299,38,true);
            TextAt(g,"当前余额  "+balance+" "+TradeCurrency,14,Col("606e55"),385,508,320,24);
            ModalButton(g,753,484,146,buy?"确认购买":"确认出售","trade-confirm",available);
        }
        void EndTradeSlider()
        {
            if(!tradeSliderHeld)return;
            tradeSliderHeld=false;
            if(Capture)Capture=false;
        }
        void SetTradeAt(float x)
        {
            int limit=CurrentTradeLimit;
            double position=Math.Max(0,Math.Min(1,(x-TradeSliderLeft)/(TradeSliderRight-TradeSliderLeft)));
            tradeQuantity=1+(int)Math.Round(position*Math.Max(0,limit-1));
        }
        void CloseTradeDetail()
        {
            EndTradeSlider();tradeItem=-1;tradeQuantity=1;hover="";
        }
        void ConfirmTrade()
        {
            bool buy=BuyingHere,sea=SellingAtHarbor;
            string name=TradeItemName;
            if(!state.TryTrade(buy,sea,tradeItem,tradeQuantity)){
                tradeQuantity=Math.Max(1,Math.Min(CurrentTradeLimit,tradeQuantity));return;
            }
            int total=tradeQuantity*FarmState.TradePrice(buy,sea,tradeItem);
            tradeNotice=(buy?"买入 ":"售出 ")+name+" × "+tradeQuantity+"，"+(buy?"花费 ":"获得 ")+total+" "+TradeCurrency;
            tradeNoticeUntil=T+3.5;
            Store();CloseTradeDetail();
        }
        bool HandleTradeAction(string a)
        {
            if(!InTradeCatalog)return false;
            if(tradeItem<0){
                const string prefix="trade-item-";
                if(!a.StartsWith(prefix))return false;
                int id;
                if(!int.TryParse(a.Substring(prefix.Length),out id)||id<0||id>(BuyingHere?FarmState.FertilizerItem:Crop.All.Length-1))return true;
                if(BuyingHere&&id!=FarmState.FertilizerItem&&!state.CropUnlocked(id))return true;
                tradeItem=id;tradeQuantity=1;tradeNoticeUntil=-1;hover="";return true;
            }
            int limit=CurrentTradeLimit;
            switch(a){
                case "close":case "trade-back":CloseTradeDetail();break;
                case "trade-slider":if(limit>1){tradeSliderHeld=true;Capture=true;SetTradeAt(mouse.X);}break;
                case "trade-less":tradeQuantity=Math.Max(1,Math.Min(limit,tradeQuantity-1));break;
                case "trade-more":tradeQuantity=Math.Max(1,Math.Min(limit,tradeQuantity+1));break;
                case "trade-confirm":ConfirmTrade();break;
            }
            return true;
        }
        bool TradeDetailKey(Keys key)
        {
            switch(key){
                case Keys.Escape:CloseTradeDetail();break;
                case Keys.Left:case Keys.Down:HandleTradeAction("trade-less");break;
                case Keys.Right:case Keys.Up:HandleTradeAction("trade-more");break;
                case Keys.Home:tradeQuantity=1;break;
                case Keys.End:tradeQuantity=Math.Max(1,CurrentTradeLimit);break;
                case Keys.Enter:ConfirmTrade();break;
                default:return false;
            }
            return true;
        }
    }
}
