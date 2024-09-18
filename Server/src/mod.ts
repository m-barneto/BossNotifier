import { DependencyContainer } from "tsyringe";
import { IPreSptLoadMod } from "@spt/models/external/IPreSptLoadMod";
import { ILogger } from "@spt/models/spt/utils/ILogger";
import { jsonc } from "jsonc";
import { VFS } from "@spt/utils/VFS";
import path from "path";
import { PreSptModLoader } from "@spt/loaders/PreSptModLoader";

class Mod implements IPreSptLoadMod {
    private config;
    preSptLoad(container: DependencyContainer): void {
        const logger = container.resolve<ILogger>("WinstonLogger");
        const vfs = container.resolve<VFS>("VFS");
        this.config = jsonc.parse(vfs.readFile(path.resolve(__dirname, "../config/config.jsonc")));
        
        const modImporter = container.resolve<PreSptModLoader>("PreSptModLoader");
        
        logger.info("Mods in order:");
        const modList = modImporter.sortModsLoadOrder();
        for (const mod in modList) {
            logger.info(modList[mod]);
        }
    }
}

export const mod = new Mod();
