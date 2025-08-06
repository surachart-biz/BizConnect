using System;
using System.Collections.Generic;

namespace BizConnect.Dal.Models;

public partial class v_dashboard_stat
{
    public long? today_total { get; set; }

    public long? today_success { get; set; }

    public long? today_failed { get; set; }

    public long? month_total { get; set; }

    public long? month_success { get; set; }

    public long? otac_generated { get; set; }

    public long? otac_validated { get; set; }

    public long? otac_used { get; set; }

    public long? active_otac { get; set; }
}
