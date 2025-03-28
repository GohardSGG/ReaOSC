namespace Loupedeck.ReaOSCPlugin.Edit
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    using Loupedeck.ReaOSCPlugin.Base;
    public class Edit_Cross_Fade : Tick_Dial_Base
    {

        public Edit_Cross_Fade()
            : base(
                displayName: "Cross Fade",
                description: "�ƶ����浭������",
                groupName: "Edit",
                increaseOSCAddress: "Crossfade/Move_Forward",
                decreaseOSCAddress: "Crossfade/Move_Backward",
                resetOSCAddress: "Crossfade/Manager")
        {
            // ���ڴ���Ӷ����ʼ��
        }

        // ��ѡ���Զ��屳��ɫ
        //protected override BitmapColor GetBackgroundColor() => new BitmapColor(0, 100, 0);
    }
}