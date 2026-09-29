namespace Mountains
{
    // Transform의 어느 값을 흔들지 고르는 공통 열거형. Punch와 Shake가 같은 세 가지
    // (위치/스케일/회전)를 지원하므로 각자 같은 enum을 두 벌 두지 않는다.
    public enum TweenChannel
    {
        Position,
        Scale,
        Rotation
    }
}
