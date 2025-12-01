using System;

[Serializable]
public class UserRecord
{
    public string username;     // 아이디
    public string passwordHash; // 암호화된 비밀번호
    public string createdAt;    // 생성 시간 (문자열로 저장)
}