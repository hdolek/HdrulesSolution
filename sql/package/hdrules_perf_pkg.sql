
-- ###############################################################
-- HDRULES_PERF_PKG - Basit performans ölçüm prosedürleri
-- ###############################################################
CREATE OR REPLACE PACKAGE HDRULES_PERF_PKG AS
  PROCEDURE TIMED_RULE_FETCH(p_group_code VARCHAR2, p_loops NUMBER DEFAULT 100, p_ms OUT NUMBER);
END HDRULES_PERF_PKG;
/
CREATE OR REPLACE PACKAGE BODY HDRULES_PERF_PKG AS
  PROCEDURE TIMED_RULE_FETCH(p_group_code VARCHAR2, p_loops NUMBER DEFAULT 100, p_ms OUT NUMBER) IS
    v_start NUMBER;
    v_end   NUMBER;
    v_dummy NUMBER;
    v_group_id NUMBER;
  BEGIN
    SELECT RULE_GROUP_ID INTO v_group_id FROM HDRULES_RULE_GROUP WHERE GROUP_CODE=p_group_code;
    v_start := DBMS_UTILITY.get_time;
    FOR i IN 1..p_loops LOOP
      SELECT COUNT(*) INTO v_dummy FROM HDRULES_RULE r WHERE r.RULE_GROUP_ID=v_group_id AND r.IS_ACTIVE=1;
      -- simulate some joins
      SELECT COUNT(*) INTO v_dummy FROM HDRULES_CONDITION c JOIN HDRULES_RULE r ON r.RULE_ID=c.RULE_ID WHERE r.RULE_GROUP_ID=v_group_id;
      SELECT COUNT(*) INTO v_dummy FROM HDRULES_RULE_OUTPUT_ROW orow JOIN HDRULES_RULE r ON r.RULE_ID=orow.RULE_ID WHERE r.RULE_GROUP_ID=v_group_id;
    END LOOP;
    v_end := DBMS_UTILITY.get_time;
    p_ms := (v_end - v_start) * 10; -- get_time returns in 1/100 sec
  END;
END HDRULES_PERF_PKG;
/
