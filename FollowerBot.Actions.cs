﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace InstagramFollowerBot
{
	public partial class FollowerBot
	{
		private static readonly Random Rand = new Random();

		private void SchroolDownLoop(int loop)
		{
			for (int i = 0; i < loop; i++)
			{
				Selenium.ScrollToBottom();
				WaitHumanizer();
			}
		}

		private void SchroolDownLoop(string divId)
		{
			string oldValue, newValue = null;
			int it = 0;
			do
			{
				oldValue = newValue;

				// accelerator
				for (int i = 0; i < it; i++)
				{
					Selenium.ScrollToBottom(divId);
				}
				it++;

				newValue = Selenium.ScrollToBottom(divId);
				WaitMin();
			}
			while (oldValue != newValue);
		}

		private void WaitMin()
		{
			Task.Delay(Config.BotStepMinWaitMs)
				.Wait();
		}
		private void WaitHumanizer()
		{
			Task.Delay(Rand.Next(Config.BotStepMinWaitMs, Config.BotStepMaxWaitMs))
					.Wait();
		}

		private void WaitUrlStartsWith(string url)
		{
			while (!Selenium.Url.StartsWith(url, StringComparison.OrdinalIgnoreCase))
			{
				Log.LogDebug("WaitUrlStartsWith...");
				WaitMin();
			}
		}

		private bool MoveTo(string partialOrNotUrl, bool forceReload = false)
		{
			Log.LogDebug("GET {0}", partialOrNotUrl);
			string target;
			if (partialOrNotUrl.StartsWith(Config.UrlRoot, StringComparison.OrdinalIgnoreCase))
			{
				target = partialOrNotUrl;
			}
			else
			{
				target = Config.UrlRoot + partialOrNotUrl;
			}
			if (!target.Equals(Selenium.Url, StringComparison.OrdinalIgnoreCase) || forceReload)
			{
				Selenium.Url = target;
				WaitHumanizer();

				// TODO error detection like "Please wait a few minutes before you try again."
				return true;
			}
			else
			{
				return true; // no redirection si OK.
			}
		}

		private void AddForced(string configName, string configValue, Queue<string> queue)
		{
			if (!string.IsNullOrWhiteSpace(configValue))
			{
				foreach (string s in configValue.Split(',', StringSplitOptions.RemoveEmptyEntries))
				{
					if (s.StartsWith(Config.UrlRoot, StringComparison.OrdinalIgnoreCase))
					{
						if (queue.Contains(s))
						{
							queue.Enqueue(s);
						}
						else
						{
							Log.LogDebug("{0} useless for {1}", configName, s);
						}
					}
					else
					{
						Log.LogWarning("Check {0} Url format for {1}", configName, s);
					}
				}
			}
		}

		private void PostAuthInit()
		{
			Selenium.ClickIfPresent(Config.CssLoginWarning);

			if (!Data.MyContactsUpdate.HasValue
				|| DateTime.UtcNow > Data.MyContactsUpdate.Value.AddHours(Config.BotCacheTimeLimitHours))
			{
				MoveTo(Data.UserContactUrl);
				WaitHumanizer();

				Selenium.Click(Config.CssContactFollowed);
				WaitHumanizer();

				// ScroolDown
				SchroolDownLoop("isgrP");

				Data.MyContacts = Selenium.GetAttributes(Config.UrlContacts)
					.ToHashSet();
				Log.LogDebug("MyContacts ={0}", Data.MyContacts.Count);

				Data.MyContactsUpdate = DateTime.UtcNow;
			}

			AddForced("AddPhotosToFav", Config.AddContactsToFollow, Data.PhotosToFav);
			AddForced("AddContactsToFav", Config.AddContactsToFav, Data.ContactsToFav);
			AddForced("AddPhotosToFav", Config.AddPhotosToFav, Data.PhotosToFav);
		}

		private void AuthLogin()
		{
			if (!MoveTo(Config.UrlLogin))
			{
				throw new NotSupportedException("INSTAGRAM RETURN ERROR ON " + Config.UrlLogin);
			}

			if (!string.IsNullOrWhiteSpace(Config.BotUserEmail))
			{
				Selenium.InputWrite(Config.CssLoginEmail, Config.BotUserEmail);

				if (!string.IsNullOrWhiteSpace(Config.BotUserPassword))
				{
					Selenium.InputWrite(Config.CssLoginPassword, Config.BotUserPassword);
					Selenium.EnterKey(Config.CssLoginPassword);
				}
				else
				{
					Log.LogWarning("Waiting user manual password validation...");
				}

				WaitUrlStartsWith(Config.UrlRoot); // loading may take some time
				WaitHumanizer(); // after WaitUrlStartsWith because 1st loading may take extra time

				// who am i ?
				Data.UserContactUrl = Selenium.GetAttributes(Config.CssLoginMyself, "href", false)
					.First(); // not single to be safe
				if (Data.UserContactUrl.EndsWith('/')) // standardize
				{
					Data.UserContactUrl = Data.UserContactUrl.Remove(Data.UserContactUrl.Length - 1);
				}
			}
			else
			{
				throw new FormatException("BotUserEmail required !");
			}
		}
		
		private void LoadCookies()
		{
			if (!MoveTo(Config.UrlRoot))
			{
				throw new NotSupportedException("INSTAGRAM RETURN ERROR ON " + Config.UrlRoot);
			}

			Selenium.Cookies = Data.Cookies; // need to have loaded the page 1st
			Selenium.SessionStorage = Data.SessionStorage; // need to have loaded the page 1st
			Selenium.LocalStorage = Data.LocalStorage; // need to have loaded the page 1st
			if (!MoveTo(Config.UrlRoot))
			{
				throw new NotSupportedException("INSTAGRAM RETURN ERROR 500 ON " + Config.UrlRoot);
			}
		}
		
		private IEnumerable<object> GetCookies()
		{
			return Selenium.Cookies;
		}
		private IDictionary<string, string> GetLocalStorage()
		{
			return Selenium.LocalStorage;
		}
		private IDictionary<string, string> GetSessionStorage()
		{
			return Selenium.SessionStorage;
		}

		private void DetectContactsFollowBack()
		{
			MoveTo(Data.UserContactUrl);
			WaitHumanizer();

			Selenium.Click("a[href*=\"/followers/\"]");
			WaitHumanizer();

			SchroolDownLoop("isgrP");
			IEnumerable<string> list = Selenium.GetAttributes(".d7ByH>a")
				.ToList();

			int c = Data.ContactsToFollow.Count;
			foreach (string needToFollow in list
				.Except(Data.MyContacts)
				.Except(Data.ContactsToFollow))
			{
				Data.ContactsToFollow.Enqueue(needToFollow);
			}
			Log.LogDebug("ContactsToFollow +{0}", Data.ContactsToFollow.Count - c);

			c = Data.ContactsToFav.Count;
			foreach (string needToFollow in list
				.Except(Data.MyContacts)
				.Except(Data.ContactsToFav))
			{
				Data.ContactsToFav.Enqueue(needToFollow);
			}
			Log.LogDebug("ContactsToFav +{0}", Data.ContactsToFav.Count - c);
		}

		private void DetectPeopleSuggested()
		{
			MoveTo(Config.UrlExplorePeopleSuggested);
			WaitHumanizer();

			SchroolDownLoop(Config.BotPeopleSuggestedScrools);

			IEnumerable<string> list = Selenium.GetAttributes("._7UhW9>a")
				.ToList();

			int c = Data.ContactsToFollow.Count;
			foreach (string needToFollow in list
				.Except(Data.MyContacts)
				.Except(Data.MyContactsBanned)
				.Except(Data.ContactsToFollow))
			{
				Data.ContactsToFollow.Enqueue(needToFollow);
			}
			Log.LogDebug("ContactsToFollow +{0}", Data.ContactsToFollow.Count - c);
		}

		private void DetectContactsUnfollowBack()
		{
			MoveTo(Data.UserContactUrl);
			WaitHumanizer();

			Selenium.Click("a.-nal3");
			WaitHumanizer();

			// ScroolDown
			SchroolDownLoop("isgrP");

			HashSet<string> contactsFollowing = Selenium.GetAttributes(Config.UrlContacts)
				.ToHashSet();

			if (Data.ContactsToUnfollow.Any())    // all data will be retried, so clear cache if required
			{
				Data.ContactsToUnfollow.Clear();
			}

			string[] result = Data.MyContacts
				.Except(contactsFollowing)
				.Except(MyContactsInTryout)
				.ToArray(); // solve
			int r = result.Length;
			if (r > 0)    // all data will be retried, so clear cache if required
			{
				if (Config.BotKeepSomeUnfollowerContacts > 0 && r > Config.BotKeepSomeUnfollowerContacts)
				{
					r -= Config.BotKeepSomeUnfollowerContacts;
				}
				foreach (string needToUnfollow in result
					.Take(r))
				{
					Data.ContactsToUnfollow.Enqueue(needToUnfollow);
				}
			}
			Log.LogDebug("ContactsToUnfollow ={0}", Data.ContactsToUnfollow.Count);
		}

		private void DoContactsFollow()
		{
			int todo = Rand.Next(Config.BotFollowTaskBatchMinLimit, Config.BotFollowTaskBatchMaxLimit);
			while (Data.ContactsToFollow.TryDequeue(out string uri) && todo > 0)
			{
				if (!MoveTo(uri))
				{
					Log.LogWarning("ACTION STOPED : INSTAGRAM RETURN ERROR ON ({0})", uri);
					break; // no retry
				}
				try
				{
					MyContactsInTryout.Add(uri);
					if (Selenium.GetElements(Config.CssContactFollow).Any()) // manage the already followed like this
					{
						Selenium.Click(Config.CssContactFollow);
						Data.MyContacts.Add(uri);
						WaitHumanizer();// the url relad may break a waiting ball

						// issue detection : Manage limit to 20 follow on a new account : https://www.flickr.com/help/forum/en-us/72157651299881165/  Then there seem to be another limit
						if (Selenium.GetElements(Config.CssContactFollow).Any()) // may be slow so will wait if required
						{
							WaitHumanizer();// give a last chance
							if (Selenium.GetElements(Config.CssContactFollow, true, true).Any())
							{
								Log.LogWarning("ACTION STOPED : SEEMS USER CAN'T FOLLOW ({0}) ANYMORE", uri);
								break; // no retry
							}
						}
						todo--;
					}
				}
				catch (Exception ex)
				{
					Log.LogWarning(default, ex, "ACTION STOPED : {0}", ex.GetBaseException().Message);
					Data.ContactsToFollow.Enqueue(uri); // try later ?
					break; // stop this action
				}
			}
		}

		private void DoContactsUnfollow()
		{
			int todo = Rand.Next(Config.BotUnfollowTaskBatchMinLimit, Config.BotUnfollowTaskBatchMaxLimit);
			while (Data.ContactsToUnfollow.TryDequeue(out string uri) && todo > 0)
			{
				if (!MoveTo(uri))
				{
					Log.LogWarning("ACTION STOPED : Instagram RETURN ERROR ({0})", uri);
					break; // no retry
				}
				try
				{
					// avec le triangle
					if (Selenium.GetElements("button.-fzfL._6VtSN").Any()) // manage the already unfollowed like this
					{
						Selenium.Click("button.-fzfL._6VtSN");
						Data.MyContacts.Remove(uri);
						MyContactsInTryout.Remove(uri);
						Selenium.Click("button.-Cab_");
						WaitHumanizer();// the url relad may break a waiting ball
						todo--;
					}
					// sans le triangle
					else if (Selenium.GetElements("button._8A5w5").Any()) // manage the already unfollowed like this
					{
						Selenium.Click("button._8A5w5");
						Data.MyContacts.Remove(uri);
						MyContactsInTryout.Remove(uri);
						Selenium.Click("button.-Cab_");
						WaitHumanizer();// the url relad may break a waiting ball
						todo--;
					}

				}
				catch (Exception ex)
				{
					Log.LogWarning(default, ex, "ACTION STOPED : {0}", ex.GetBaseException().Message);
					break; // stop this action
				}
			}
		}

	}
}