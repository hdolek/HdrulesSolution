
using System;

namespace Hdrules.Domain;

// DTO/POCO'lar - DB kolon adlarıyla aynı (alias gerektirmez)
public class HDRULES_RULE_GROUP {
    public long RULE_GROUP_ID { get; set; }
    public string GROUP_CODE { get; set; } = "";
    public string GROUP_NAME { get; set; } = "";
    public string? DESCRIPTION { get; set; }
    public int VERSION_NO { get; set; }
    public int IS_ACTIVE { get; set; }
    public DateTime? VALID_FROM { get; set; }
    public DateTime? VALID_TO { get; set; }
    public string CACHE_STRATEGY { get; set; } = "MEMORY";
    public int CACHE_TTL_SEC { get; set; } = 300;
    public DateTime? CREATED_AT { get; set; }
    public DateTime? UPDATED_AT { get; set; }
}

public class HDRULES_RULE {
    public long RULE_ID { get; set; }
    public long RULE_GROUP_ID { get; set; }
    public string RULE_CODE { get; set; } = "";
    public string RULE_NAME { get; set; } = "";
    public int PRIORITY { get; set; }
    public int VERSION_NO { get; set; }
    public int IS_ACTIVE { get; set; }
    public DateTime? VALID_FROM { get; set; }
    public DateTime? VALID_TO { get; set; }
    public string? NOTES { get; set; }
}

public class HDRULES_INPUT_PARAM_DEF {
    public long INPUT_PARAM_ID { get; set; }
    public long? RULE_GROUP_ID { get; set; }
    public string PARAM_CODE { get; set; } = "";
    public string? DISPLAY_NAME { get; set; }
    public string DATA_TYPE { get; set; } = "STRING";
    public string? JSON_PATH { get; set; }
    public string? DESCRIPTION { get; set; }
    public int IS_REQUIRED { get; set; }
}

public class HDRULES_OUTPUT_COL_DEF {
    public long OUTPUT_COL_ID { get; set; }
    public long RULE_GROUP_ID { get; set; }
    public string COL_CODE { get; set; } = "";
    public string? DISPLAY_NAME { get; set; }
    public string DATA_TYPE { get; set; } = "STRING";
    public string? DESCRIPTION { get; set; }
    public int IS_REQUIRED { get; set; }
}

public class HDRULES_OUTPUT_COL_ATTR_DEF {
    public long OUTPUT_ATTR_ID { get; set; }
    public long OUTPUT_COL_ID { get; set; }
    public string ATTR_CODE { get; set; } = "";
    public string? ATTR_VALUE { get; set; }
}

public class HDRULES_CONDITION {
    public long CONDITION_ID { get; set; }
    public long RULE_ID { get; set; }
    public int ORDINAL { get; set; }
    public string? LEFT_PARAM_CODE { get; set; }
    public string? LEFT_JSON_PATH { get; set; }
    public string? LEFT_TRANSFORMS { get; set; }
    public string OPERATOR { get; set; } = "EQ";
    public string VALUE_TYPE { get; set; } = "SCALAR";
    public string? VALUE_TEXT { get; set; }
    public string? VALUE_TO_TEXT { get; set; }
    public int CASE_SENSITIVE { get; set; }
    public int NEGATE { get; set; }
}

public class HDRULES_COND_SET_VALUE {
    public long COND_SET_VAL_ID { get; set; }
    public long CONDITION_ID { get; set; }
    public string VALUE_TEXT { get; set; } = "";
}

public class HDRULES_RULE_OUTPUT_ROW {
    public long OUTPUT_ROW_ID { get; set; }
    public long RULE_ID { get; set; }
    public int ROW_NO { get; set; }
    public int IS_DEFAULT { get; set; }
    public DateTime? VALID_FROM { get; set; }
    public DateTime? VALID_TO { get; set; }
}

public class HDRULES_RULE_OUTPUT_CELL {
    public long OUTPUT_CELL_ID { get; set; }
    public long OUTPUT_ROW_ID { get; set; }
    public long OUTPUT_COL_ID { get; set; }
    public string SOURCE_TYPE { get; set; } = "CONST";
    public string? CONST_VALUE { get; set; }
    public string? JSON_PATH { get; set; }
    public string? API_CODE { get; set; }
    public string? API_MAP_PATH { get; set; }
    public string? NRULES_CODE { get; set; }
    public string? TRANSFORM_CHAIN { get; set; }
    public string? NOTES { get; set; }
}

public class HDRULES_RULE_OUTPUT_CELL_ATTR {
    public long OUTPUT_CELL_ID { get; set; }
    public string ATTR_CODE { get; set; } = "";
    public string? ATTR_VALUE { get; set; }
}

public class HDRULES_API_DEF {
    public long API_ID { get; set; }
    public string API_CODE { get; set; } = "";
    public string NAME { get; set; } = "";
    public string METHOD { get; set; } = "GET";
    public string URL { get; set; } = "";
    public string? HEADERS { get; set; }
    public string? BODY_TEMPLATE { get; set; }
    public int TIMEOUT_SEC { get; set; } = 10;
    public string? DESCRIPTION { get; set; }
}

public class HDRULES_RULE_API_BINDING {
    public long RULE_ID { get; set; }
    public long API_ID { get; set; }
    public string WHEN_TO_CALL { get; set; } = "ON_MATCH";
}

public class HDRULES_NRULES_DEF {
    public long NRULES_ID { get; set; }
    public long? RULE_GROUP_ID { get; set; }
    public string NRULES_CODE { get; set; } = "";
    public string NAME { get; set; } = "";
    public string CSHARP_CODE { get; set; } = "";
    public int IS_ACTIVE { get; set; }
    public DateTime? VALID_FROM { get; set; }
    public DateTime? VALID_TO { get; set; }
    public string? NOTES { get; set; }
}
