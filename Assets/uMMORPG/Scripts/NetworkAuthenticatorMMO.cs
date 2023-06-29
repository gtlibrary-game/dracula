using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Mirror;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.ServerModels;
using System.Collections.Generic;
using Thirdweb;

public class TempToken {
    public string Token { get; }
    public DateTime Expiration { get; }

    public TempToken() {
        // Generate a random token string
        Token = Guid.NewGuid().ToString();

        // Set expiration to 10 minutes from now
        Expiration = DateTime.UtcNow.AddMinutes(10);
    }

    public bool IsExpired() {
        return DateTime.UtcNow >= Expiration;
    }
}


public class NetworkAuthenticatorMMO : NetworkAuthenticator
{
    [Header("Components")]
    public NetworkManagerMMO manager;
    public NFTManager nftManager;
    // login info for the local player
    // we don't just name it 'account' to avoid collisions in handshake
    [Header("Login")]
    public string loginAccount = "";
    public string loginPassword = "";

    [Header("Security")]
    public string passwordSalt = "at_least_16_byte";
    public int accountMaxLength = 256;
    public string playFabId;
    public string sessionTicket;
    public string signedTicket = null;
    public Task ClientWalletSign;
    public bool LoginFlg =false;
    [Header("Events")]
    public UnityEvent OnSignedClientCallback;
    public UnityEvent OnFailedSignClientCallback;


    public async void SignAndSendTicket() {
        print("===========SignAndSendTicket=========="+signedTicket);
        // try{
                
        //     if(signedTicket == null)
        //     {
        // print(nftManager.mintButtonFlg);
        if(nftManager.mintButtonFlg == true){
                string walletAddress = await ThirdwebManager.Instance.SDK.wallet.GetAddress();
                signedTicket = await ThirdwebManager.Instance.SDK.wallet.Sign(sessionTicket);
                SignTicketMsg message = new SignTicketMsg {
                    account=loginAccount,
                    playFabId=playFabId,
                    sessionTicket=sessionTicket,
                    signedTicket=signedTicket,
                };
                NetworkClient.connection.Send(message);
        }
        //     }
        // }catch(Exception e){
        //     Debug.LogWarning($"Error SignAndSendTicket: {e}");
        // }
       
    }

    public void RegisterEmail() {
        var request = new RegisterPlayFabUserRequest {
            Email = loginAccount,
            Password = loginPassword,
            RequireBothUsernameAndEmail = false
        }; 
        
        PlayFabClientAPI.RegisterPlayFabUser(request, OnRegisterSuccess, OnError);
        Debug.Log("In register");
    }
    //void OnRegisterSuccess(RegisterPlayFabUserResult result) {
    void OnRegisterSuccess(RegisterPlayFabUserResult result)
    {
        Debug.LogWarning("You are now registred.");
        // PlayFabClientAPI.Logout();
    }
    void OnError(PlayFabError error) {
        Debug.LogWarning(error);
        manager.uiPopup.Show("" + error);
    }

    public void LoginUser() {

        Debug.Log("In LoginUser:"+loginAccount);
        var request = new LoginWithEmailAddressRequest {
            Email = loginAccount,
            Password = loginPassword
        };
        PlayFabClientAPI.LoginWithEmailAddress(request, OnLoginSuccess, OnError);
    }
    public async void OnLoginSuccess(LoginResult result) {

        try{
            playFabId = result.PlayFabId;
            sessionTicket = result.SessionTicket;
            Debug.Log("Logged in with PlayFab ID: " + playFabId);
            Debug.Log("Session ticket: " + sessionTicket);

            // // FIXME: We need to move the call to this code into the two stage sign in process --jrr
            // ThirdwebManager.Instance.SDK.wallet.Sign(sessionTicket);
            // FIXME: We need to move these calls into the two-stage sign-in process --jrr
            // ClientWalletSign = ThirdwebManager.Instance.SDK.wallet.Sign(sessionTicket);
            // signedTicketTask.Wait();
            // signedTicket = signedTicketTask.Result;

            // OnStartClientAfterPlayFab
            OnSignedClientCallback?.Invoke();
        }
        catch (Exception e)
        {
            OnFailedSignClientCallback?.Invoke();
            Debug.LogWarning($"Error Sign Client: {e}");
            
        }
    }

    public async void OnStartClientAfterPlayFab(){
        try{
            print("==========OnStartClientAfterPlayFab=========");
            // var signResult = await ClientWalletSign;
            // print(signResult);
            manager.StartClient();
            // OnClientAuthenticate();
        }
        catch (Exception e)
        {
            
        }
    }

    public void OnClientWalletConnect() {
        if (null == sessionTicket) {
            return; // We will try to sign the ticket when it is ready.
        } else {
            // FIXME:  Sign the sessionTicket and send SignedTicketMsg {} to the server.
        }
    }

    public void ResetPassword() {
        var request = new SendAccountRecoveryEmailRequest {
            Email = loginAccount,
            TitleId = "C0C36"
        };
        PlayFabClientAPI.SendAccountRecoveryEmail(request, OnPasswordReset, OnError);
    }
    public void OnPasswordReset(SendAccountRecoveryEmailResult result) {
        Debug.Log("Password recovery email sent.");
    }


    // client //////////////////////////////////////////////////////////////////
    public override void OnStartClient()
    {
        // register login success message, allowed before authenticated
        NetworkClient.RegisterHandler<LoginSuccessMsg>(OnClientLoginSuccess, false);
        //NetworkClient.RegisterHandler<LoginWrongUser>(OnLoginWrongUserResult, false);
        //NetworkClient.RegisterHandler<RegisterSuccessMsg>(OnRegisterResult, false);
        
    }

    public override void OnClientAuthenticate()
    {
        print("=================OnClientAuthenticate================");
        // send login packet with hashed password, so that the original one
        // never leaves the player's computer.
        //
        // it's recommended to use a different salt for each hash. ideally we
        // would store each user's salt in the database. to not overcomplicate
        // things, we will use the account name as salt (at least 16 bytes)
        //
        // Application.version can be modified under:
        // Edit -> Project Settings -> Player -> Bundle Version
        string hash = Utils.PBKDF2Hash(loginPassword, passwordSalt + loginAccount);
        // -------------------------Develop mode-----------------------//
        // LoginMsg message = new LoginMsg{account=loginAccount, password=hash, version=Application.version};
        //--------------------------------------------------------------//
        LoginMsg message = new LoginMsg{account=loginAccount, password=hash, version=Application.version, playFabId=playFabId, sessionTicket=sessionTicket}; //, signedTicket=signedTicket};
        NetworkClient.connection.Send(message);
        Debug.Log("login message was sent");

        // set state
        manager.state = NetworkState.Handshake;
    }

    void OnClientLoginSuccess(LoginSuccessMsg msg)
    {
        print("==========OnClientLoginSuccess===========");
        //------- Production mode----------//
        // SignAndSendTicket();
        //---------------------------------//
        // authenticated successfully. OnClientConnected will be called.
        OnClientAuthenticated.Invoke();
    }

    void OnRegisterResult(RegisterSuccessMsg msg)
    {
        print(msg.msg);
    }
    void OnLoginWrongUserResult(LoginWrongUser msg)
    {
        print(msg.msg);
    }
    public void OnClientRegister()
    {
        // string hash = Utils.PBKDF2Hash(loginPassword, passwordSalt + loginAccount);
        // string characterName = manager.charactersAvailableMsg.characters[manager.selection].name;
        // RegisterMsg message = new RegisterMsg{account=characterName, password="hash", version=Application.version};
        // NetworkClient.connection.Send(message);
        // Debug.Log("Register message was sent");
        // manager.state = NetworkState.Handshake;
    }

     public async void LoadServerWallet() {
        string walletAddress = "0x6f72eaEeaBd8c5d5ef1E1b7fc9355969Dd834E52";
        Thirdweb.Utils.UnlockOrGenerateLocalAccount(43113, "password", "2b6c8223500c5b312df77739fb323c3df18a52f485d4ba199137151674ee9896", walletAddress);

        WalletConnection wc = new WalletConnection (WalletProvider.LocalWallet, 43113, "password");
        ThirdwebManager.Instance.SDK.wallet.Connect(wc);

        var signature = await ThirdwebManager.Instance.SDK.wallet.Sign("Mesg");

        
        // FIXME: Send the game owner wallet one loot of id 1 to verify the game contracts are working.
        // ThirdwebManager.mintLoot(..walletddress...);

    }   
    // server //////////////////////////////////////////////////////////////////
    public override void OnStartServer()
    {
        // StartHost3
        // register login message, allowed before authenticated
        NetworkServer.RegisterHandler<LoginMsg>(OnServerLogin, false);
        NetworkServer.RegisterHandler<SignTicketMsg>(OnSignTicket, false);
        NetworkServer.RegisterHandler<RegisterMsg>(OnServerRegister, false);
        // NetworkServer.RegisterHandler<ResetPasswordMsg>(OnServerResetPassword, false);
        LoadServerWallet();
    }

    public override void OnServerAuthenticate(NetworkConnectionToClient conn)
    {
        // wait for LoginMsg from client

    }

    // virtual in case someone wants to modify
    public virtual bool IsAllowedAccountName(string account)
    {
        return true;
        // not too long?
        // only contains letters, number and underscore and not empty (+)?
        // (important for database safety etc.)
        //return account.Length <= accountMaxLength &&
               //Regex.IsMatch(account, @"^[a-zA-Z0-9_\.]+$");
    }

    bool AccountLoggedIn(string account)
    {
        // in lobby or in world?
        return manager.lobby.ContainsValue(account) || Player.onlinePlayers.Values.Any(p => p.account == account);
    }

    void OnServerRegister(NetworkConnectionToClient conn, RegisterMsg message)
    {

        if (message.version == Application.version)
        {
            // allowed account name?
            if (IsAllowedAccountName(message.account))
            {

                print(message.account);
                //    Database.singleton.TryRegister(message.account);
                CharacterStats myCharacter = Database.singleton.GetCharacterStats(message.account);
                print(myCharacter.name);
                // if ()
                // {
                //    conn.Send(new RegisterSuccessMsg{ msg = "Register has been successful!" });
                // }else{
                //     conn.Send(new RegisterSuccessMsg{ msg = "You already has been registered" });
                // }
            }
        }
    }
    // void OnServerResetPassword(NetworkConnectionToClient conn, ResetPasswordMsg message)
    // {

    // }
    public Dictionary<string, NetworkConnectionToClient> ticketToConn = new Dictionary<string, NetworkConnectionToClient>();
    public Dictionary<string, string> playFabIdToTicket = new Dictionary<string, string>();
    public Dictionary<string, string> playFabIdToSigned = new Dictionary<string, string>();
    public Dictionary<string, string> playFabIdToAccount = new Dictionary<string, string>();


    private async void SetWalletAddressFromSignedTicket(string playFabId, string account) {
        string address = await ThirdwebManager.Instance.SDK.wallet.RecoverAddress(playFabIdToTicket[playFabId], playFabIdToSigned[playFabId]);
        Debug.LogWarning("Setting the following address: " + address);
        Debug.LogWarning("Account: " + account);
        // FIXME: we should send back an all clear message saying that the account is linked.
    }

    void OnAuthenticateSignedTicket(AuthenticateSessionTicketResult result) {
        NetworkConnectionToClient conn = ticketToConn[result.UserInfo.PlayFabId];

        string account = playFabIdToAccount[result.UserInfo.PlayFabId];

        SetWalletAddressFromSignedTicket(result.UserInfo.PlayFabId, account);
    }

    void OnSignTicket(NetworkConnectionToClient conn, SignTicketMsg message) {
        Debug.Log("Signing playfab session ticket...");
        ticketToConn[message.playFabId] = conn;
        playFabIdToTicket[message.playFabId] = message.sessionTicket;
        playFabIdToSigned[message.playFabId] = message.signedTicket;
        playFabIdToAccount[message.playFabId] = message.account;

        var request = new AuthenticateSessionTicketRequest {
            SessionTicket = message.sessionTicket
        };

        PlayFabServerAPI.AuthenticateSessionTicket(request, OnAuthenticateSignedTicket, OnError , conn);
    }

    void OnAuthenticateSessionTicket(AuthenticateSessionTicketResult result) {
        Debug.Log("Playfab authenticated...");

        NetworkConnectionToClient conn = ticketToConn[result.UserInfo.PlayFabId];

        // login successful
        Debug.Log("login successful: " + result.UserInfo.PlayFabId);

        // notify client about successful login. otherwise it
        // won't accept any further messages.
        conn.Send(new LoginSuccessMsg());

        // FIXME: Need to save the signedTicket's account address to the database associating it with the email address.
        // FIXME: https://portal.thirdweb.com/unity/wallet/recoveraddress
        // string address = ThirdwebManager...Get XXX

        //string address = await ThirdwebManager.Instance.SDK.wallet.RecoverAddress(playFabIdToTicket[result.UserInfo.PlayFabId], playFabIdToSigned[result.UserInfo.PlayFabId]);
        // authenticate on server
        OnServerAuthenticated.Invoke(conn);
    }

    void OnServerLogin(NetworkConnectionToClient conn, LoginMsg message)
    {
        print("=======================NetworkConnectionToClient==========================");
        // correct version?
        if (message.version == Application.version)
        {
            // allowed account name?
            if (IsAllowedAccountName(message.account))
            {

                // validate account info
                if (Database.singleton.TryLogin(message.account, "message.password")) // Always true because we are using playfab to check the passwords. --JRR
                {
       
                    // not in lobby and not in world yet?
                    if (!AccountLoggedIn(message.account))
                    {

                        print("=======if (!AccountLoggedIn(message.account))======");
                        // add to logged in accounts
                        manager.lobby[conn] = message.account;

                        // Need to check if the playFab sessionTicket matches the account
                        var request = new AuthenticateSessionTicketRequest {
            	            SessionTicket = message.sessionTicket
        	            };
                        
                        //----------------------------Production mode----------------------------------//
                        ticketToConn[message.playFabId] = conn;
                        playFabIdToTicket[message.playFabId] = message.sessionTicket;
                        PlayFabServerAPI.AuthenticateSessionTicket(request, OnAuthenticateSessionTicket, OnError , conn);
                        //------------------------------------------------------------------------------//
                        // login successful
                        Debug.Log("login successful: " + message.account);

                        // Debug.Log("result.IsSessionTicketExpired: " + result.IsSessionTicketExpired);

                        // notify client about successful login. otherwise it
                        // won't accept any further messages.
                        //----------------------------Development Mode----------------------------------//
                        // conn.Send(new LoginSuccessMsg());
                        // authenticate on server
                        // OnServerAuthenticated.Invoke(conn);
                        //------------------------------------------------------------------------------//
                    }
                    else
                    {
                        Debug.Log("account already logged in: " + message.account);
                        manager.ServerSendError(conn, "already logged in", true);
                        // note: we should disconnect the client here, but we can't as
                        // long as unity has no "SendAllAndThenDisconnect" function,
                        // because then the error message would never be sent.
                        conn.Disconnect();
                    }
                }
            }
            else
            {
                //Debug.Log("account name not allowed: " + message.account); <- don't show on live server
                manager.ServerSendError(conn, "account name not allowed", true);
            }
        }
        else
        {
            //Debug.Log("version mismatch: " + message.account + " expected:" + Application.version + " received: " + message.version); <- don't show on live server
            manager.ServerSendError(conn, "outdated version", true);
        }
    }
}
