
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Data;
using Dapper;
using Hdrules.Domain;

namespace Hdrules.Data;

public class DecisionRepository
{
    private readonly DbConnectionFactory _factory;
    public DecisionRepository(DbConnectionFactory factory) => _factory = factory;

    public async Task<HDRULES_RULE_GROUP?> GetGroupByCodeAsync(string groupCode)
    {
        using var db = _factory.Create();
        return (await db.QueryAsync<HDRULES_RULE_GROUP>(
            "SELECT * FROM HDRULES_RULE_GROUP WHERE GROUP_CODE=:p", new { p = groupCode })).FirstOrDefault();
    }

    public async Task<IEnumerable<HDRULES_INPUT_PARAM_DEF>> GetInputParamsAsync(long groupId)
    {
        using var db = _factory.Create();
        return await db.QueryAsync<HDRULES_INPUT_PARAM_DEF>(
            "SELECT * FROM HDRULES_INPUT_PARAM_DEF WHERE RULE_GROUP_ID=:g OR RULE_GROUP_ID IS NULL ORDER BY PARAM_CODE", new { g = groupId });
    }

    public async Task<IEnumerable<HDRULES_OUTPUT_COL_DEF>> GetOutputColsAsync(long groupId)
    {
        using var db = _factory.Create();
        return await db.QueryAsync<HDRULES_OUTPUT_COL_DEF>(
            "SELECT * FROM HDRULES_OUTPUT_COL_DEF WHERE RULE_GROUP_ID=:g ORDER BY COL_CODE", new { g = groupId });
    }

    public async Task<IEnumerable<HDRULES_OUTPUT_COL_ATTR_DEF>> GetOutputColAttrsAsync(IEnumerable<long> outputColIds)
    {
        using var db = _factory.Create();
        return await db.QueryAsync<HDRULES_OUTPUT_COL_ATTR_DEF>(
            "SELECT * FROM HDRULES_OUTPUT_COL_ATTR_DEF WHERE OUTPUT_COL_ID IN :ids", new { ids = outputColIds.ToArray() });
    }

    public async Task<IEnumerable<HDRULES_RULE>> GetActiveRulesAsync(long groupId)
    {
        using var db = _factory.Create();
        return await db.QueryAsync<HDRULES_RULE>(@"
            SELECT * FROM HDRULES_RULE
             WHERE RULE_GROUP_ID=:g AND IS_ACTIVE=1
               AND (VALID_FROM IS NULL OR VALID_FROM <= SYSDATE)
               AND (VALID_TO   IS NULL OR VALID_TO   >= SYSDATE)
             ORDER BY PRIORITY DESC, RULE_ID", new { g = groupId });
    }

    public async Task<IEnumerable<HDRULES_CONDITION>> GetConditionsAsync(IEnumerable<long> ruleIds)
    {
        using var db = _factory.Create();
        return await db.QueryAsync<HDRULES_CONDITION>(
            "SELECT * FROM HDRULES_CONDITION WHERE RULE_ID IN :ids ORDER BY RULE_ID, ORDINAL", new { ids = ruleIds.ToArray() });
    }

    public async Task<IEnumerable<HDRULES_COND_SET_VALUE>> GetCondSetValuesAsync(IEnumerable<long> condIds)
    {
        using var db = _factory.Create();
        return await db.QueryAsync<HDRULES_COND_SET_VALUE>(
            "SELECT * FROM HDRULES_COND_SET_VALUE WHERE CONDITION_ID IN :ids", new { ids = condIds.ToArray() });
    }

    public async Task<IEnumerable<HDRULES_RULE_OUTPUT_ROW>> GetOutputRowsAsync(IEnumerable<long> ruleIds)
    {
        using var db = _factory.Create();
        return await db.QueryAsync<HDRULES_RULE_OUTPUT_ROW>(
            "SELECT * FROM HDRULES_RULE_OUTPUT_ROW WHERE RULE_ID IN :ids ORDER BY RULE_ID, ROW_NO", new { ids = ruleIds.ToArray() });
    }

    public async Task<IEnumerable<HDRULES_RULE_OUTPUT_CELL>> GetOutputCellsAsync(IEnumerable<long> outputRowIds)
    {
        using var db = _factory.Create();
        return await db.QueryAsync<HDRULES_RULE_OUTPUT_CELL>(
            "SELECT * FROM HDRULES_RULE_OUTPUT_CELL WHERE OUTPUT_ROW_ID IN :ids ORDER BY OUTPUT_ROW_ID, OUTPUT_CELL_ID", new { ids = outputRowIds.ToArray() });
    }

    public async Task<IEnumerable<HDRULES_RULE_OUTPUT_CELL_ATTR>> GetOutputCellAttrsAsync(IEnumerable<long> cellIds)
    {
        using var db = _factory.Create();
        return await db.QueryAsync<HDRULES_RULE_OUTPUT_CELL_ATTR>(
            "SELECT * FROM HDRULES_RULE_OUTPUT_CELL_ATTR WHERE OUTPUT_CELL_ID IN :ids", new { ids = cellIds.ToArray() });
    }

    public async Task<IEnumerable<HDRULES_API_DEF>> GetApiDefsAsync()
    {
        using var db = _factory.Create();
        return await db.QueryAsync<HDRULES_API_DEF>("SELECT * FROM HDRULES_API_DEF");
    }

    public async Task<IEnumerable<HDRULES_NRULES_DEF>> GetNRulesAsync(string? code = null)
    {
        using var db = _factory.Create();
        if (string.IsNullOrEmpty(code))
            return await db.QueryAsync<HDRULES_NRULES_DEF>(@"SELECT * FROM HDRULES_NRULES_DEF WHERE IS_ACTIVE=1");
        return await db.QueryAsync<HDRULES_NRULES_DEF>(@"SELECT * FROM HDRULES_NRULES_DEF WHERE IS_ACTIVE=1 AND NRULES_CODE=:c", new { c = code });
    }
}
