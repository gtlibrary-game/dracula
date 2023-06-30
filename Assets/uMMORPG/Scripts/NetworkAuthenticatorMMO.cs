using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Mirror;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.ServerModels;
using System.Collections.Generic;
using Thirdweb;
//using UCompile;
using MoonSharp.Interpreter;
using OpenAI;
using System.Net.Http;

public class NetworkAuthenticatorMMO : NetworkAuthenticator
{
    [Header("Components")]
    public NetworkManagerMMO manager;

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
    public string signedTicket;


    [Serializable]
    public struct FromChatWrapper {
        public FromChat content;
        public string type;
    };
    [Serializable]
    public struct FromChat {
        public string role;
        public string content;
    };
    public async void LoadScripts() {

        //Debug.Log("In Load Scripts");
        string url = "https://author.greatlibrary.io/art/chat/";
        using (HttpClient client = new HttpClient())
        {
            var content = new MultipartFormDataContent
            {
                //{ new StringContent(chatId), "chatId" },
                //{ new StringContent(sdkId), "" },
                { new StringContent("True"), "return_json" },
                { new StringContent("I write moonsharp lua code."), "context" },
                { new StringContent("Hello computer."), "user_input" },
                //{ new StringContent(message1), "message1" },
                //{ new StringContent(message2), "message2" }
            };

            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            string result = await response.Content.ReadAsStringAsync();
            Debug.Log("result from DKC " + result);
            result = JsonUtility.FromJson<FromChatWrapper>(result).content.content;
            Debug.Log(result);
        }
/*
        OpenAIApi openai = new OpenAIApi();
        List<OpenAI.ChatMessage> messages = new List<OpenAI.ChatMessage>();

        var newMessage = new OpenAI.ChatMessage() {
                Role = "user",
                Content = "hello there computer?"
        };

        messages.Add(newMessage);

        // Complete the instruction  // See Johnrraymond for { api_key: "sk-...." }  ->  %USERPROFILE%\.openai\auth.json 
        // See https://github.com/srcnalt/OpenAI-Unity
        var completionResponse = await openai.CreateChatCompletion(new CreateChatCompletionRequest() {
                Model = "gpt-3.5-turbo-0301",
                Messages = messages
        });

        if (completionResponse.Choices != null && completionResponse.Choices.Count > 0) {
            var message = completionResponse.Choices[0].Message;
            message.Content = message.Content.Trim();

            Debug.Log("AI message is: " + message.Content);
        }
                
*/
	    string script = @"    
		-- defines a factorial function
		function fact (n)
			if (n == 0) then
				return 1
			else
				return n*fact(n - 1)
			end
		end


		return fact(5)";

	    DynValue res = Script.RunString(script);
	    Debug.LogWarning("result: " + res.Number);
    
    }

    public async void SignAndSendTicket() {
        string walletAddress = await ThirdwebManager.Instance.SDK.wallet.GetAddress();
        print(walletAddress);
        
        signedTicket = await ThirdwebManager.Instance.SDK.wallet.Sign(sessionTicket);
        
        SignTicketMsg message = new SignTicketMsg {
            account=loginAccount,
            playFabId=playFabId,
            sessionTicket=sessionTicket,
            signedTicket=signedTicket,
        };
        NetworkClient.connection.Send(message);
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
    void OnRegisterSuccess(RegisterPlayFabUserResult result) {
        Debug.LogWarning("You are now registred.");
        // PlayFabClientAPI.Logout();
    }
    void OnError(PlayFabError error) {
        Debug.LogWarning(error);
        manager.uiPopup.Show("" + error);
    }

    public void LoginUser() {
        Debug.Log("In LoginUser");
        var request = new LoginWithEmailAddressRequest {
            Email = loginAccount,
            Password = loginPassword
        };
        PlayFabClientAPI.LoginWithEmailAddress(request, OnLoginSuccess, OnError);
    }
    public void OnLoginSuccess(LoginResult result) {
        playFabId = result.PlayFabId;
        sessionTicket = result.SessionTicket;

        Debug.Log("Logged in with PlayFab ID: " + playFabId);
        Debug.Log("Session ticket: " + sessionTicket);

        // FIXME: We need to move these calls into the two-stage sign-in process --jrr
        //var signedTicketTask = ThirdwebManager.Instance.SDK.wallet.Sign(sessionTicket);
        //signedTicketTask.Wait();
        //signedTicket = signedTicketTask.Result;

        //Debug.LogWarning("signedTicket: " + signedTicket);

        manager.StartClient();
        OnClientAuthenticate();
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
        LoginMsg message = new LoginMsg{account=loginAccount, password=hash, version=Application.version, playFabId=playFabId, sessionTicket=sessionTicket}; //, signedTicket=signedTicket};
        NetworkClient.connection.Send(message);
        Debug.Log("login message was sent");

        // set state
        manager.state = NetworkState.Handshake;
    }

    void OnClientLoginSuccess(LoginSuccessMsg msg)
    {
        SignAndSendTicket();
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
        /*
        string hash = Utils.PBKDF2Hash(loginPassword, passwordSalt + loginAccount);
        RegisterMsg message = new RegisterMsg{account=loginAccount, password=hash, version=Application.version, playFabId=playFabId, sessionTicket=sessionTicket};
        NetworkClient.connection.Send(message);
        Debug.Log("Register message was sent");
        manager.state = NetworkState.Handshake;
        */
    }
    
    public async void LoadServerWallet() {
        string walletAddress = "0x6f72eaEeaBd8c5d5ef1E1b7fc9355969Dd834E52";
        Thirdweb.Utils.UnlockOrGenerateLocalAccount(43113, "password", "2b6c8223500c5b312df77739fb323c3df18a52f485d4ba199137151674ee9896", walletAddress);

        WalletConnection wc = new WalletConnection (WalletProvider.LocalWallet, 43113, "password");
        ThirdwebManager.Instance.SDK.wallet.Connect(wc);

        string signature = await ThirdwebManager.Instance.SDK.wallet.Sign("Mesg");
        print("Signature: " + signature);

        // FIXME: Send the game owner wallet one loot of id 1 to verify the game contracts are working.
        // ThirdwebManager.mintLoot(..walletddress...);

    }

    // server //////////////////////////////////////////////////////////////////
    public override void OnStartServer()
    {
        // register login message, allowed before authenticated
        NetworkServer.RegisterHandler<LoginMsg>(OnServerLogin, false);
        NetworkServer.RegisterHandler<SignTicketMsg>(OnSignTicket, false);
        //NetworkServer.RegisterHandler<RegisterMsg>(OnServerRegister, false);
        // NetworkServer.RegisterHandler<ResetPasswordMsg>(OnServerResetPassword, false);

        LoadServerWallet();

        LoadScripts();
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
        return manager.lobby.ContainsValue(account) ||
               Player.onlinePlayers.Values.Any(p => p.account == account);
    }

    void OnServerRegister(NetworkConnectionToClient conn, RegisterMsg message)
    {
        /*
        if (message.version == Application.version)
        {
            // allowed account name?
            if (IsAllowedAccountName(message.account))
            {
               
                if (Database.singleton.TryRegister(message.account, message.password))
                {
                   conn.Send(new RegisterSuccessMsg{ msg = "Register has been successful!" });
                }else{
                    conn.Send(new RegisterSuccessMsg{ msg = "You already has been registered" });
                }
            }
        }
        */
    }
    // void OnServerResetPassword(NetworkConnectionToClient conn, ResetPasswordMsg message)
    // {

    // }
    Dictionary<string, NetworkConnectionToClient> ticketToConn = new Dictionary<string, NetworkConnectionToClient>();
    Dictionary<string, string> playFabIdToTicket = new Dictionary<string, string>();
    Dictionary<string, string> playFabIdToSigned = new Dictionary<string, string>();
    Dictionary<string, string> playFabIdToAccount = new Dictionary<string, string>();

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
                        // add to logged in accounts
                        manager.lobby[conn] = message.account;

                        // Need to check if the playFab sessionTicket matches the account
                        var request = new AuthenticateSessionTicketRequest {
            	            SessionTicket = message.sessionTicket
        	            };
                        
                        ticketToConn[message.playFabId] = conn;
                        playFabIdToTicket[message.playFabId] = message.sessionTicket;
                        //playFabIdToSigned[message.playFabId] = message.signedTicket;

                        PlayFabServerAPI.AuthenticateSessionTicket(request, OnAuthenticateSessionTicket, OnError , conn);

                        // login successful
                        //Debug.Log("login successful: " + message.account);

                        //Debug.Log("result.IsSessionTicketExpired: " + result.IsSessionTicketExpired);
                        

                        // login successful
                        //Debug.Log("login successful: " + message.account);

                        // notify client about successful login. otherwise it
                        // won't accept any further messages.
                        //conn.Send(new LoginSuccessMsg());

                        // authenticate on server
                        //OnServerAuthenticated.Invoke(conn);
                    }
                    else
                    {
                        Debug.Log("account already logged in: " + message.account);
                        manager.ServerSendError(conn, "already logged in", true);

                        // note: we should disconnect the client here, but we can't as
                        // long as unity has no "SendAllAndThenDisconnect" function,
                        // because then the error message would never be sent.
                        //conn.Disconnect();
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
