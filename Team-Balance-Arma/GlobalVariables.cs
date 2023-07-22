using System;

namespace TeamBalanceArma
{
    public static class GlobalVariables
    {
        // To-do
        /*
        private static readonly string dbFolder = @"";
        public static readonly string dbFolder = Environment.ExpandEnvironmentVariables(dbFolderUserProfile);
        */

        // ******************************************************************************************************
        // ATTENTION! DON'T USE IN PRODUCTION YET, password needs to be moved to more secure environment variable
        // ******************************************************************************************************
        // // Also the database name itself could be an env var just as a bit of extra security measure
        // See also important notes in the comments below - to avoid confusion, it would be a good idea to use username other than root
        public static readonly string DbLocationAndDetails = @"server=localhost;uid=root;pwd=YOURPASSWORDHERE;database=YOURDBNAMEHERE";

        public static readonly string LogsFolder = @"@Database\Logs\";

        public static readonly long SideWest = 1;
        public static readonly long SideEast = 2;

        public static readonly Random Random = new Random();


        // Procedures to create in the MySQL database application (or command line)
        // We don't let Arma to call procedures directly since it would be a severe security risk (SQL injection!)
        // Also don't let even this DLL itself call SQL freely, but define procedures in the database itself and disable everything else (whitelist the procedures only) instead
        // Remember to disable every other/unneeded privilege of the user accessing the database!
        // Also disable WAN, even LAN access to the database since we use it only locally (in this project)! (Whitelist localhost only)

        /*
        private static readonly string createMainTableIfDoesNotExistSQLString = @"CREATE TABLE IF NOT EXISTS guids(id INTEGER PRIMARY KEY AUTO_INCREMENT, guid BIGINT NOT NULL UNIQUE)";
        private static readonly string createScoreTableIfDoesNotExistSQLString = @"CREATE TABLE IF NOT EXISTS players(id BIGINT PRIMARY KEY AUTO_INCREMENT, player_guid BIGINT NOT NULL, score SMALLINT NOT NULL DEFAULT 0, side VARCHAR(30), FOREIGN KEY (player_guid) REFERENCES guids(guid))";
        private static readonly string checkIfPlayerExists = @"CREATE PROCEDURE check_if_player_exists(IN guid BIGINT, OUT result INT) SELECT COUNT(1) INTO result FROM players WHERE player_guid = guid";
        private static readonly string addPlayer = @"CREATE PROCEDURE add_player(IN player_guid BIGINT) BEGIN IF NOT (EXISTS (SELECT 1 FROM `guids` WHERE guid=player_guid LIMIT 1)) THEN INSERT INTO guids (guid) VALUES (player_guid); END IF; END";
        private static readonly string isNewPlayer = @"CREATE PROCEDURE init_player_data(IN guid BIGINT, IN currentScore SMALLINT) INSERT INTO players(player_guid, score) Values (guid, currentScore)";
        private static readonly string addPlayerScore = @"CREATE PROCEDURE add_player_score(IN player_score SMALLINT, IN player_side VARCHAR(30), IN guid BIGINT) BEGIN UPDATE players SET side=NULL WHERE player_guid=guid; INSERT INTO players(player_guid, score, side) VALUES(guid, player_score, player_side); END";
        private static readonly string removeOldestPlayerScore = @"CREATE PROCEDURE remove_oldest_player_score(IN guid BIGINT) DELETE FROM players WHERE player_guid = guid ORDER BY id ASC LIMIT 1";
        private static readonly string retrievePlayerTotalScore = @"CREATE PROCEDURE retrieve_player_total_score(IN guid BIGINT, OUT totalScore INT) SELECT SUM(score) INTO totalScore FROM players WHERE player_guid = guid AND score IS NOT NULL";
        private static readonly string retrievePlayerTotalTicks = @"CREATE PROCEDURE retrieve_player_total_ticks(IN guid BIGINT, OUT totalTicks INT) SELECT COUNT(score) INTO totalTicks FROM players WHERE player_guid = guid";
        private static readonly string retrievePlayerSide = @"CREATE PROCEDURE retrieve_player_side(IN guid BIGINT, OUT playerSide VARCHAR(30)) SELECT side INTO playerSide FROM players WHERE player_guid=guid AND side IS NOT NULL ORDER BY id DESC LIMIT 1";
        private static readonly string addPlayerSide = @"CREATE PROCEDURE add_player_side(IN guid BIGINT, IN playerSide VARCHAR(30)) UPDATE players SET side=playerSide WHERE player_guid = guid ORDER BY id DESC LIMIT 1";
        private static readonly string getAllActivePlayers = @"CREATE PROCEDURE get_active_players() SELECT * FROM players WHERE side='WEST' OR side='EAST'";
        private static readonly string getActivePlayersSide = @"CREATE PROCEDURE get_active_players_side(IN requestedSide VARCHAR(30)) SELECT * FROM players WHERE side=requestedSide";
        private static readonly string flushSideOfAllPlayers = @"CREATE PROCEDURE flush_players() UPDATE players SET side=NULL";
        private static readonly string setMap = @"CREATE PROCEDURE set_map() (IN currentMap VARCHAR(60)) INSERT INTO map(map) VALUES (currentMap)";
        private static readonly string getMap = @"CREATE PROCEDURE get_map() (OUT currentMapOut VARCHAR(60)) SELECT * FROM map WHERE map IS NOT NULL LIMIT 1 INTO currentMapOut";
        */

    }

}