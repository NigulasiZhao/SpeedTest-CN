namespace SpeedTest_CN.Models.PmisAndZentao;

public class PMISInfo
{
    public string Url { get; set; }
    public string Authorization { get; set; }
}

public class GetByDateAndUserIdResponse
{
    public int Code { get; set; }
    public bool Success { get; set; }
    public Response? Response { get; set; }
    public string Message { get; set; }
}

public class Response
{
    public string id { get; set; }
    public string userId { get; set; }
    public string userName { get; set; }
    public string systemId { get; set; }
    public string systemName { get; set; }
    public string groupId { get; set; }
    public string groupName { get; set; }
    public string fillDate { get; set; }
    public long? submitTime { get; set; }
    public int? status { get; set; }
    public int? hasFile { get; set; }
    public string responsibility1 { get; set; }
    public string responsibilityFile1 { get; set; }
    public string responsibility2 { get; set; }
    public string responsibilityFile2 { get; set; }
    public string responsibility3 { get; set; }
    public string responsibilityFile3 { get; set; }
    public string responsibility4 { get; set; }
    public string responsibilityFile4 { get; set; }
    public string responsibility5 { get; set; }
    public string responsibilityFile5 { get; set; }
    public string responsibility6 { get; set; }
    public string responsibilityFile6 { get; set; }
    public string responsibility7 { get; set; }
    public string responsibilityFile7 { get; set; }
    public string responsibility8 { get; set; }
    public string responsibilityFile8 { get; set; }
    public string responsibility9 { get; set; }
    public string responsibilityFile9 { get; set; }
    public string otherContent { get; set; }
    public string otherFile { get; set; }
    public string suggestContent { get; set; }
    public int? hasComment { get; set; }
    public int? hasNewComment { get; set; }
    public string created { get; set; }
    public string updater { get; set; }
    public string creater { get; set; }
    public string updated { get; set; }
    public int? syncPms { get; set; }
    public string beginDate { get; set; }
    public string endDate { get; set; }
    public string dataRange { get; set; }
    public bool restDay { get; set; }
    public string viewStatus { get; set; }
    public bool takeRest { get; set; }
    public string checkInRule { get; set; }
    public List<ClockRecord> goWorks { get; set; }
    public List<ClockRecord> offWorks { get; set; }
    public Overtime overtime { get; set; }
    public bool haveData { get; set; }
    public bool commentAuth { get; set; }
    public object responsibilityList { get; set; }
    public List<Detail> details { get; set; }
    public object comments { get; set; }
    public string recipientId { get; set; }
    public string recipientName { get; set; }
    public string ccTo { get; set; }
    public string ccToName { get; set; }
}

public class ClockRecord
{
    public string time { get; set; }
    public int clockInStatus { get; set; }
    public string clockInStatusName { get; set; }
}

public class Overtime
{
    public string overTime { get; set; }
    public List<object> timeRange { get; set; }
}

public class Detail
{
    public string id { get; set; }
    public string userId { get; set; }
    public string description { get; set; }
    public string target { get; set; }
    public int? useStatus { get; set; }
    public string taskConfirmTime { get; set; }
    public string taskName { get; set; }
    public string planBeginTime { get; set; }
    public string planFinishTime { get; set; }
    public string planFinishAct { get; set; }
    public string responsibility { get; set; }
    public string workType { get; set; }
    public string clientName { get; set; }
    public string realJob { get; set; }
    public string realFinishAct { get; set; }
    public string file { get; set; }
    public string summary { get; set; }
    public string suggest { get; set; }
    public string remark { get; set; }
    public int status { get; set; }
    public string actTime { get; set; }
    public string actId { get; set; }
    public string beginDate { get; set; }
    public string endDate { get; set; }
    public string customerName { get; set; }
    public string contractNumber { get; set; }
    public double workHours { get; set; }
    public string customerId { get; set; }
    public int ztTaskId { get; set; }
    public string wimpicFile { get; set; }
    public string travelId { get; set; }
    public string travelBusinessKey { get; set; }
}

public class PMISInsertResponse
{
    public int Code { get; set; }
    public string Message { get; set; }
    public bool Success { get; set; }
}