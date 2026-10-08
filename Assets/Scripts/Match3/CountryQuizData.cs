namespace Match3
{
    /// <summary>
    /// 나라찾기 게임에 나오는 100개 나라 (ISO 3166-1 alpha-2 코드 + 한국어 이름).
    /// 국기 이미지는 Assets/Resources/Sprites/Flags/&lt;코드&gt;.png (flagcdn.com, 퍼블릭 도메인).
    /// 나라를 추가하려면 여기 한 줄 + 같은 코드 이름의 국기 png를 넣으면 된다.
    /// </summary>
    public static class CountryQuizData
    {
        public readonly struct Country
        {
            public readonly string Code;
            public readonly string Name;

            public Country(string code, string name)
            {
                Code = code;
                Name = name;
            }
        }

        public static readonly Country[] Countries =
        {
            new Country("kr", "대한민국"),
            new Country("jp", "일본"),
            new Country("cn", "중국"),
            new Country("mn", "몽골"),
            new Country("tw", "대만"),
            new Country("vn", "베트남"),
            new Country("th", "태국"),
            new Country("ph", "필리핀"),
            new Country("id", "인도네시아"),
            new Country("my", "말레이시아"),
            new Country("sg", "싱가포르"),
            new Country("kh", "캄보디아"),
            new Country("la", "라오스"),
            new Country("mm", "미얀마"),
            new Country("in", "인도"),
            new Country("pk", "파키스탄"),
            new Country("bd", "방글라데시"),
            new Country("np", "네팔"),
            new Country("lk", "스리랑카"),
            new Country("bt", "부탄"),
            new Country("kz", "카자흐스탄"),
            new Country("uz", "우즈베키스탄"),
            new Country("af", "아프가니스탄"),
            new Country("ir", "이란"),
            new Country("iq", "이라크"),
            new Country("sa", "사우디아라비아"),
            new Country("ae", "아랍에미리트"),
            new Country("qa", "카타르"),
            new Country("kw", "쿠웨이트"),
            new Country("il", "이스라엘"),
            new Country("jo", "요르단"),
            new Country("lb", "레바논"),
            new Country("sy", "시리아"),
            new Country("tr", "튀르키예"),
            new Country("ge", "조지아"),
            new Country("gb", "영국"),
            new Country("ie", "아일랜드"),
            new Country("fr", "프랑스"),
            new Country("de", "독일"),
            new Country("it", "이탈리아"),
            new Country("es", "스페인"),
            new Country("pt", "포르투갈"),
            new Country("nl", "네덜란드"),
            new Country("be", "벨기에"),
            new Country("ch", "스위스"),
            new Country("at", "오스트리아"),
            new Country("dk", "덴마크"),
            new Country("no", "노르웨이"),
            new Country("se", "스웨덴"),
            new Country("fi", "핀란드"),
            new Country("is", "아이슬란드"),
            new Country("pl", "폴란드"),
            new Country("cz", "체코"),
            new Country("sk", "슬로바키아"),
            new Country("hu", "헝가리"),
            new Country("ro", "루마니아"),
            new Country("bg", "불가리아"),
            new Country("gr", "그리스"),
            new Country("hr", "크로아티아"),
            new Country("rs", "세르비아"),
            new Country("ua", "우크라이나"),
            new Country("ru", "러시아"),
            new Country("ee", "에스토니아"),
            new Country("lv", "라트비아"),
            new Country("lt", "리투아니아"),
            new Country("mt", "몰타"),
            new Country("cy", "키프로스"),
            new Country("us", "미국"),
            new Country("ca", "캐나다"),
            new Country("mx", "멕시코"),
            new Country("cu", "쿠바"),
            new Country("jm", "자메이카"),
            new Country("gt", "과테말라"),
            new Country("cr", "코스타리카"),
            new Country("pa", "파나마"),
            new Country("br", "브라질"),
            new Country("ar", "아르헨티나"),
            new Country("cl", "칠레"),
            new Country("pe", "페루"),
            new Country("co", "콜롬비아"),
            new Country("ve", "베네수엘라"),
            new Country("uy", "우루과이"),
            new Country("py", "파라과이"),
            new Country("bo", "볼리비아"),
            new Country("ec", "에콰도르"),
            new Country("eg", "이집트"),
            new Country("ma", "모로코"),
            new Country("dz", "알제리"),
            new Country("tn", "튀니지"),
            new Country("ng", "나이지리아"),
            new Country("gh", "가나"),
            new Country("ke", "케냐"),
            new Country("et", "에티오피아"),
            new Country("za", "남아프리카공화국"),
            new Country("sn", "세네갈"),
            new Country("cm", "카메룬"),
            new Country("tz", "탄자니아"),
            new Country("au", "오스트레일리아"),
            new Country("nz", "뉴질랜드"),
            new Country("fj", "피지"),
        };
    }
}
