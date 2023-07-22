# team-balancing-arma

## What is this project about?

This extension is an "intelligent" (self-learning) and fully autonomous team balancing system that improves its estimates of players' skills constantly and autonomously. It needs to be combined with a properly set up MySQL database.

The self-learning and autonomous skill measure system parameters can be adjusted, but the default values (data from the last ~17 hours (1000 minutes)) should be the sweet spot between accuracy and prevention of "estimation lag". (It's a common issue in e.g. measuring human performance: if you use measures from too long period of time, you don't have an accurate measurement of the _current_ performance and if you use too real-time data, the result becomes statistically (too) unreliable). It's up to you to determine the balance between accurate enough measurement and tolerable level of statistical reliability in your project.

It's worth noting that after getting the parameters right, this system worked amazingly well in the original project where it was used. (See IMPORTANT section below).

## Database data

The extension handles the following data via it's MySQL database:

- extensive player scores needed to implement team balancing script
- the currently played map/terrain/island.

## Notes

- Consider this project rather as a WIP showcase than a recommended application to use before fixing the things mentioned below (see IMPORTANT section below).
- You need to implement the procedure codes in the calling SQF code yourself.
- The score counting needs to be balanced in the gamemode/mod/mission itself, since this extension bases it's balancing on the ingame scores of players.
- Remember to follow the standard security measures (see comments in `GlobalVariables.cs` for more info). And again, see the IMPORTANT section below, too.
- You also need to sync the project data with the database manually initially (see IMPORTANT section below).

## IMPORTANT!
- There are some remaining (very) bad practices left like not using a proper ORM (EF Framework) due to lack of time to learn it. You should implement an ORM before using this project in production! There's a real risk of data corruption and need of extensive ingame testing in it's current state because the project code (e.g. `Player.cs` object/class variables) and database have to be synced manually.
- Also the database password, name and accessing user needs to be moved to more secure environment variable (WIP due to lack of time)!
- When it comes to the the mod/gamemode/mission itself, you should use special caution to handle player score counting securely (e.g. keep the whole logic server side and use `CfgFunctions` in Arma 3 for better security). See my `CfgFunctions` generator plugin for Visual Studio Code here: https://marketplace.visualstudio.com/items?itemName=Ezcoo.cfgfunctionsgeneratorarma3 or use my platform agnostic `CfgFunctions` generation Python script: https://github.com/Ezcoo/cfgfunctions-generator-py (more info about `CfgFunctions` in Arma 3 Wiki/BIKI: https://community.bistudio.com/wiki/Arma_3:_Functions_Library )
- You might need to change the parameters of the entry point of this extension (`Dispatch.cs` main method) if you use this project in Arma 3.
- The internal logging system used in this project should be really be replaced with external dependency to follow the best practices in development (and to avoid ugly fixes in the source code) – consider the logging system itself more as a showcase (apart from the circular dependency avoidance parts of it).