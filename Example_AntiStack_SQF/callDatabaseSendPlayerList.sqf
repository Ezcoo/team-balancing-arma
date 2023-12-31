// Call to database
private ["_procedureName","_procedureCode","_parameters","_uid","_score","_response","_responseCode","_responseTotalScore","_responseTicks","_playerSkill","_responseStats","_isArray","_parametersTemp","_side"];

_procedureName = _this select 0;
_parameters = _this select 1;

_parametersTemp = "";

_guid = "";
_side = west;
_sideAsNumber = 0;

// diag_log format ["_procedureName: %1, _parameters: %2", _procedureName, _parameters];

{
	_guid = _x select 0;

	// diag_log format ["_guid: %1, _parametersTemp == '': %2", _guid, (_parametersTemp == "")];


	// Don't add dot in the beginning of the string formatted array
	if (_parametersTemp == "") then {
		_parametersTemp = _guid + ",";
	} else {
		_parametersTemp = _parametersTemp + "," + _guid + ","
	};

	_side = _x select 1;

	if (_side == west) then {
		_sideAsNumber = 1;
	};

	if (_side == east) then {
		_sideAsNumber = 2;
	};

	_parametersTemp = _parametersTemp + str _sideAsNumber;

} forEach _parameters;

_parameters = _parametersTemp;

_parametersTemp = "";

_response = {};

_procedureCode = "";

if (_procedureName == "SEND_PLAYERLIST") then {
	_procedureCode = 303;
	["INFORMATION", format ["CallDatabaseSendPlayerList.sqf: Calling database with procedure: [%1]. Parameters: [%2].", _procedureName, _parameters]] Call WFBE_CO_FNC_LogContent;
	_response = "A2WaspDatabase" callExtension format ["%1,%2",_procedureCode,_parameters];
};

["INFORMATION", format ["CallDatabaseSendPlayerList.sqf: Called database successfully with procedure: [%1].", _procedureName]] Call WFBE_CO_FNC_LogContent;

_response = call compile _response;

_responseCode = _response select 0;

if (_responseCode < 0) then {
	["ERROR", format ["CallDatabaseSendPlayerList.sqf: CRITICAL ERROR! Something went wrong with database, check it's error logs. Procedure name: [%1].", _procedureName]] Call WFBE_CO_FNC_LogContent;
};