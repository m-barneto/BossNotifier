import { DependencyContainer, inject, injectable } from "tsyringe";
import { IPreSptLoadMod } from "@spt/models/external/IPreSptLoadMod";
import { ILogger } from "@spt/models/spt/utils/ILogger";
import { jsonc } from "jsonc";
import { VFS } from "@spt/utils/VFS";
import path from "path";
import { PreSptModLoader } from "@spt/loaders/PreSptModLoader";
import { StaticRouterModService } from "@spt/services/mod/staticRouter/StaticRouterModService";

@injectable()
class Mod implements IPreSptLoadMod {
    private config;
    private fikaMatchService;
    preSptLoad(container: DependencyContainer): void {
        const logger = container.resolve<ILogger>("WinstonLogger");
        const vfs = container.resolve<VFS>("VFS");
        this.config = jsonc.parse(vfs.readFile(path.resolve(__dirname, "../config/config.jsonc")));
        
        const modImporter = container.resolve<PreSptModLoader>("PreSptModLoader");
        const hasFika = modImporter.getImportedModsNames().includes("fika-server");
        //if (!hasFika) return;

        const staticRouterModService = container.resolve<StaticRouterModService>("StaticRouterModService");

        staticRouterModService.registerStaticRouter(
            "BossNotifierRouter",
            [
                {
                    url: "/bosses/",
                    action: async (url, info, sessionId, output) => {
                        logger.info(url);
                        logger.info(info);
                        logger.info(sessionId);
                        logger.info(output);
                        return JSON.stringify({ bosses: [ "urmom", "cthulu", "jeff" ]});
                    }
                }
            ],
            "bossnotifier"
        )
    }
}

export const mod = new Mod();
