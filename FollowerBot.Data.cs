﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace InstagramFollowerBot
{
	public partial class FollowerBot
	{

		private class PersistenceData
		{
			public string UserContactUrl = null;

			/// <summary>
			/// List of URL of the contact'photos
			/// </summary>
			public Queue<string> ContactsToFav = new Queue<string>();

			/// <summary>
			/// List of URL of the contact'photos
			/// </summary>        
			public Queue<string> ContactsToFollow = new Queue<string>();

			/// <summary>
			/// List of URL of the contact'photos
			/// </summary>
			public Queue<string> PhotosToFav = new Queue<string>();

			/// <summary>
			/// List of URL of the contact'photos
			/// </summary>
			public Queue<string> ContactsToUnfollow = new Queue<string>();

			public IEnumerable<object> Cookies = new List<object>();
			public IDictionary<string, string> SessionStorage = new Dictionary<string, string>();
			public IDictionary<string, string> LocalStorage = new Dictionary<string, string>();
			
			/// <summary>
			/// Last refresh date
			/// </summary>   
			public Nullable<DateTime> MyContactsUpdate = null;

			/// <summary>
			/// List of URL of the contact'photos
			/// </summary>        
			public HashSet<string> MyContacts = new HashSet<string>();

			/// <summary>
			/// List of URL of the contact'photos
			/// </summary>        
			public HashSet<string> MyContactsBanned = new HashSet<string>();

		}

		/// <summary>
		/// Not banned if don t follow back
		/// </summary>
		private readonly HashSet<string> MyContactsInTryout = new HashSet<string>();

		private string JsonPath;

		private PersistenceData Data;

		private void LoadData()
		{
			Data = new PersistenceData();
			if (Config.BotUsePersistence)
			{
				if (!string.IsNullOrWhiteSpace(Config.BotUserSaveFolder))
				{
					JsonPath = Config.BotUserSaveFolder;
					if (!JsonPath.EndsWith('\\'))
					{
						JsonPath += '\\';
					}
					if (!Directory.Exists(Config.BotUserSaveFolder))
					{
						try
						{
							Directory.CreateDirectory(JsonPath);
						}
						catch (Exception ex)
						{
							Log.LogError(default, ex, "Coundn't create {0} directory, using current.", JsonPath);
							JsonPath = ExecPath + "\\PersistenceData_";
						}
					}
				}
				else
				{
					JsonPath = ExecPath + "\\PersistenceData_";
				}

				if (File.Exists(JsonPath + Config.BotUserEmail + ".json"))
				{
					Log.LogDebug("LOADING USER JSON");
					PersistenceData tmp = JsonConvert.DeserializeObject<PersistenceData>(File.ReadAllText(JsonPath + Config.BotUserEmail + ".json", Encoding.UTF8));
					Data.UserContactUrl = tmp.UserContactUrl;
					if (Config.BotCacheMyContacts)
					{
						Data.MyContactsUpdate = tmp.MyContactsUpdate;
						if (tmp.MyContacts != null)
						{
							Data.MyContacts = tmp.MyContacts;
							Log.LogDebug("MyContacts : {0}", Data.MyContacts.Count);
						}
						if (tmp.MyContactsBanned != null)
						{
							Data.MyContactsBanned = tmp.MyContactsBanned;
							Log.LogDebug("MyContactsBanned : {0}", Data.MyContactsBanned.Count);
						}
					}
					if (tmp.ContactsToFollow != null)
					{
						Data.ContactsToFollow = new Queue<string>(tmp.ContactsToFollow
							.Except(Data.MyContacts).Except(Data.MyContactsBanned)); // some contacts may have been already added manualy
						Log.LogDebug("ContactsToFollow :  {0}", Data.ContactsToFollow.Count);
					}
					if (tmp.ContactsToFav != null)
					{
						Data.ContactsToFav = new Queue<string>(tmp.ContactsToFav
							.Except(Data.MyContacts).Except(Data.MyContactsBanned)); // some contacts may have been already added manualy
						Log.LogDebug("ContactsToFav :  {0}", Data.ContactsToFav.Count);
					}
					if (tmp.ContactsToUnfollow != null)
					{
						Data.ContactsToUnfollow = tmp.ContactsToUnfollow;
						Log.LogDebug("ContactsToUnfollow :  {0}", Data.ContactsToUnfollow.Count);
					}
					if (tmp.PhotosToFav != null)
					{
						Data.PhotosToFav = tmp.PhotosToFav;
						Log.LogDebug("PhotosToFav :  {0}", Data.PhotosToFav.Count);
					}
					if (tmp.Cookies != null)
					{
						Data.Cookies = tmp.Cookies;
						Log.LogDebug("Cookies : {0}", Data.Cookies.Count());
					}
					if (tmp.SessionStorage != null)
					{
						Data.SessionStorage = tmp.SessionStorage;
						Log.LogDebug("SessionStorage : {0}", Data.SessionStorage.Count);
					}
					if (tmp.LocalStorage != null)
					{
						Data.LocalStorage = tmp.LocalStorage;
						Log.LogDebug("LocalStorage : {0}", Data.LocalStorage.Count);
					}
				}
			}
		}

		private void SaveData()
		{
			if (Config.BotUsePersistence)
			{
				Log.LogDebug("SAVING USER JSON");
				PersistenceData tmp = new PersistenceData()
				{
					UserContactUrl = Data.UserContactUrl,
					ContactsToFollow = Data.ContactsToFollow,
					ContactsToFav = Data.ContactsToFav,
					ContactsToUnfollow = Data.ContactsToUnfollow,
					PhotosToFav = Data.PhotosToFav,
					Cookies = GetCookies(),
					SessionStorage = GetSessionStorage(),
					LocalStorage = GetLocalStorage()
				};
				if (Config.BotCacheMyContacts)
				{
					tmp.MyContactsUpdate = Data.MyContactsUpdate;
					tmp.MyContacts = Data.MyContacts;
					tmp.MyContactsBanned = Data.MyContactsBanned;
				}
				File.WriteAllText(JsonPath + Config.BotUserEmail + ".json", JsonConvert.SerializeObject(tmp, Formatting.Indented), Encoding.UTF8);
			}
		}

	}
}