import { inject, injectable } from "tsyringe";

import { LocationController } from "@spt/controllers/LocationController";
import { ILogger } from "@spt/models/spt/utils/ILogger";
import { SaveServer } from "@spt/servers/SaveServer";

export declare class FikaMatchService {
    protected matches: Map<string, IFikaMatch>;
    protected timeoutIntervals: Map<string, NodeJS.Timeout>;

    constructor(
        logger: ILogger,
        locationController: LocationController,
        saveServer: SaveServer,
        fikaConfig: FikaConfig,
        fikaDedicatedRaidService: FikaDedicatedRaidService,
    );

    /**
     * Adds a timeout interval for the given match
     * @param matchId
     */
    private addTimeoutInterval(matchId: string): void;

    /**
     * Removes the timeout interval for the given match
     * @param matchId
     * @returns
     */
    private removeTimeoutInterval(matchId: string): void;

    /**
     * Returns the match with the given id, undefined if match does not exist
     * @param matchId
     * @returns
     */
    public getMatch(matchId: string): IFikaMatch;

    /**
     * Returns all matches
     * @returns
     */
    public getAllMatches(): Map<string, IFikaMatch>;

    /**
     * Returns all match ids
     * @returns
     */
    public getAllMatchIds(): string[];

    /**
     * Returns the player with the given id in the given match, undefined if either match or player does not exist
     * @param matchId
     * @param playerId
     * @returns
     */
    public getPlayerInMatch(matchId: string, playerId: string): IFikaPlayer;

    /**
     * Returns an array with all playerIds in the given match, undefined if match does not exist
     *
     * Note:
     * - host player is the one where playerId is equal to matchId
     * @param matchId
     * @returns
     */
    public getPlayersIdsByMatch(matchId: string): string[];

    /**
     * Returns the match id that has a player with the given player id, undefined if the player isn't in a match
     *
     * @param playerId
     * @returns
     */
    public getMatchIdByPlayer(playerId: string): string;

    /**
     * Returns the match id that has a player with the given session id, undefined if the player isn't in a match
     *
     * Note:
     * - First tries to find pmc, then scav
     * @param sessionId
     * @returns
     */
    public getMatchIdByProfile(sessionId: string): string;

    /**
     * Creates a new coop match
     * @param data
     * @returns
     */
    public createMatch(data: IFikaRaidCreateRequestData): boolean;

    /**
     * Deletes a coop match and removes the timeout interval
     * @param matchId
     */
    public deleteMatch(matchId: string): void;

    /**
     * Ends the given match, logs a reason and removes the timeout interval
     * @param matchId
     * @param reason
     */
    public endMatch(matchId: string, reason: FikaMatchEndSessionMessage): void;

    /**
     * Updates the status of the given match
     * @param matchId
     * @param status
     */
    public setMatchStatus(matchId: string, status: FikaMatchStatus): void;

    /**
     * Sets the ip and port for the given match
     * @param matchId
     * @param ips
     * @param port
     */
    public setMatchHost(matchId: string, ips: string[], port: number, natPunch: boolean, isDedicated: boolean): void;

    /**
     * Resets the timeout of the given match
     * @param matchId
     */
    public resetTimeout(matchId: string): void;

    /**
     * Adds a player to a match
     * @param matchId
     * @param playerId
     * @param data
     */
    public addPlayerToMatch(matchId: string, playerId: string, data: IFikaPlayer): void;

    /**
     * Sets a player to dead
     * @param matchId
     * @param playerId
     * @param data
     */
    public setPlayerDead(matchId: string, playerId: string): void;

    /**
     * Sets the groupId for a player
     * @param matchId
     * @param playerId
     * @param groupId
     */
    public setPlayerGroup(matchId: string, playerId: string, groupId: string): void;

    /**
     * Removes a player from a match
     * @param matchId
     * @param playerId
     */
    public removePlayerFromMatch(matchId: string, playerId: string): void;
}