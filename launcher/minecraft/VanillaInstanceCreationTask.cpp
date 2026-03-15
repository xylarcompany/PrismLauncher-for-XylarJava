#include "VanillaInstanceCreationTask.h"

#include <utility>
#include <QDir>
#include <QFile>

#include "FileSystem.h"
#include "minecraft/MinecraftInstance.h"
#include "minecraft/PackProfile.h"
#include "settings/INISettingsObject.h"

VanillaCreationTask::VanillaCreationTask(BaseVersion::Ptr version, QString loader, BaseVersion::Ptr loader_version)
    : InstanceCreationTask()
    , m_version(std::move(version))
    , m_using_loader(true)
    , m_loader(std::move(loader))
    , m_loader_version(std::move(loader_version))
{}

std::unique_ptr<MinecraftInstance> VanillaCreationTask::createInstance()
{
    setStatus(tr("Creating instance from version %1").arg(m_version->name()));

    auto inst = std::make_unique<MinecraftInstance>(m_globalSettings, std::make_unique<INISettingsObject>(FS::PathCombine(m_stagingPath, "instance.cfg")),
                           m_stagingPath);
    SettingsObject::Lock lock(inst->settings());

    auto components = inst->getPackProfile();
    components->buildingFromScratch();
    components->setComponentVersion("net.minecraft", m_version->descriptor(), true);
    if (m_using_loader)
        components->setComponentVersion(m_loader, m_loader_version->descriptor());

    inst->setName(name());
    inst->setIconKey(m_instIcon);

    // XylarJava Customization: Add pre-installed mods
    addXylarJavaMods(inst.get());

    return inst;
}

void VanillaCreationTask::addXylarJavaMods(MinecraftInstance* inst)
{
    // Create mods folder if it doesn't exist
    QString modsFolder = inst->modsRoot();
    QDir modsDir(modsFolder);
    if (!modsDir.exists()) {
        modsDir.mkpath(".");
    }

    // XylarJava mods to pre-install
    QStringList xylarMods = {
        "xylarservers-4.4.9-SNAPSHOT.jar",
        "ViaBedrock-0.0.26-SNAPSHOT.jar"
    };

    // For now, we just ensure the mods folder exists
    // The actual mod files should be bundled with the launcher or downloaded
    // This is a placeholder that allows for future mod auto-download
    setStatus(tr("Setting up mods for XylarJava"));
}
