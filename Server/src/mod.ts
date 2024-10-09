import { DependencyContainer, inject, injectable } from "tsyringe";
import { IPreSptLoadMod } from "@spt/models/external/IPreSptLoadMod";
import { ILogger } from "@spt/models/spt/utils/ILogger";
import { jsonc } from "jsonc";
import { VFS } from "@spt/utils/VFS";
import path from "path";
import { PreSptModLoader } from "@spt/loaders/PreSptModLoader";
import { StaticRouterModService } from "@spt/services/mod/staticRouter/StaticRouterModService";
import { FikaMatchService } from "./FikaMatchService";

@injectable()
class Mod implements IPreSptLoadMod {
    private config;
    private logger: ILogger;
    private fikaMatchService: FikaMatchService;

    //                        matchId        boss    location
    private bossesInMatch: Map<string, Record<string, string>> = new Map();

    preSptLoad(container: DependencyContainer): void {
        this.logger = container.resolve<ILogger>("WinstonLogger");
        const vfs = container.resolve<VFS>("VFS");
        this.config = jsonc.parse(vfs.readFile(path.resolve(__dirname, "../config/config.jsonc")));
        
        const modImporter = container.resolve<PreSptModLoader>("PreSptModLoader");
        const hasFika = modImporter.getImportedModsNames().includes("fika-server");
        if (hasFika) {
            this.fikaMatchService = container.resolve<FikaMatchService>("FikaMatchService");
        }

        const staticRouterModService = container.resolve<StaticRouterModService>("StaticRouterModService");

        staticRouterModService.registerStaticRouter(
            "BossNotifierRouter",
            [
                {
                    url: "/getbosses/",
                    action: async (url, info, sessionId, output) => {
                        const matchId = this.fikaMatchService.getMatchIdByProfile(sessionId);
                        if (!this.matchHasBossList(matchId)) {
                            // idk tbh, return some empty list of bosses?
                            this.logger.info("No match found for this dude, kill him!");
                            return JSON.stringify({ bosses: {}});
                        }

                        const bossList = this.getBossesInMatch(matchId);
                        this.logger.success(`Sending boss list to client! ${JSON.stringify({ bosses: bossList})}`)

                        return JSON.stringify({ bosses: bossList});
                    }
                },
                {
                    url: "/setbosses/",
                    action: async (url, info, sessionId, output) => {
                        const bossList: Record<string, string> = {};
                        Object.keys(info).forEach(key => {
                            bossList[key] = info[key];
                        });
                        this.logger.info(JSON.stringify(bossList));
                        this.setBossListForMatch(sessionId, bossList);
                        this.logger.info(JSON.stringify(this.bossesInMatch[sessionId]));
                        return JSON.stringify({response: "OK"});
                    }
                }
            ],
            "bossnotifier"
        );
    }

    printMatches(): void {
        for (const matchId in this.bossesInMatch) {
            this.logger.info(matchId);
            this.logger.info(this.bossesInMatch[matchId]);
        }
    }

    matchHasBossList(matchId: string): boolean {
        return this.bossesInMatch[matchId] !== undefined;
    }

    setBossListForMatch(matchId: string, bossesInMatch: Record<string, string>): void {
        this.bossesInMatch[matchId] = bossesInMatch;
    }

    getBossesInMatch(matchId: string): Record<string, string> | undefined {
        if (!this.matchHasBossList(matchId)) {
            return undefined;
        }

        return this.bossesInMatch[matchId];
    }
}

export const mod = new Mod();
