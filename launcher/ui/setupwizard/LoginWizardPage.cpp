#include "LoginWizardPage.h"
#include "minecraft/auth/AccountList.h"
#include "minecraft/auth/MinecraftAccount.h"
#include "ui/dialogs/ChooseOfflineNameDialog.h"
#include "ui_LoginWizardPage.h"

#include "Application.h"

LoginWizardPage::LoginWizardPage(QWidget* parent) : BaseWizardPage(parent), ui(new Ui::LoginWizardPage)
{
    ui->setupUi(this);
}

LoginWizardPage::~LoginWizardPage()
{
    delete ui;
}

void LoginWizardPage::initializePage() {}

bool LoginWizardPage::validatePage()
{
    return true;
}

void LoginWizardPage::retranslate()
{
    ui->retranslateUi(this);
}

void LoginWizardPage::on_pushButton_clicked()
{
    // XylarJava: Use offline account instead of Microsoft
    ChooseOfflineNameDialog dialog(tr("Please enter your desired username for XylarJava."), nullptr);
    if (dialog.exec() == QDialog::Accepted) {
        if (const MinecraftAccountPtr account = MinecraftAccount::createOffline(dialog.getUsername())) {
            account->login()->start();
            APPLICATION->accounts()->addAccount(account);
            APPLICATION->accounts()->setDefaultAccount(account);
            if (wizard()->currentId() == wizard()->pageIds().last()) {
                wizard()->accept();
            } else {
                wizard()->next();
            }
        }
    }
}
