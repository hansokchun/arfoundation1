/// <summary>
/// 씬이 변경되어도 파괴되지 않는 정적(Static) 변수입니다.
/// 스캔 씬에서 배치 씬으로 job_id를 전달하는 유일한 통로입니다.
/// </summary>
public static class JobDataHolder
{
    public static string LatestJobID = null;
}