#region References
using System;

using Server.Accounting;
using Server.Items;
using Server.Mobiles;
using Server.Network;
#endregion

namespace Server.Misc
{
	public class CharacterCreation
	{
		private static readonly CityInfo m_NewHavenInfo = new CityInfo(
			"New Haven",
			"The Bountiful Harvest Inn",
			3503,
			2574,
			14,
			Map.Trammel);

		private static readonly CityInfo m_SiegeInfo = new CityInfo(
			"Britain",
			"The Wayfarer's Inn",
			1075074,
			1602,
			1591,
			20,
			Map.Felucca);

		private static Mobile m_Mobile;

		public static void Initialize()
		{
			// Register our event handler
			EventSink.CharacterCreated += EventSink_CharacterCreated;
		}

		public static bool VerifyProfession(int profession)
		{
			if (profession < 0)
				return false;
			if (profession < 4)
				return true;
			if (Core.AOS && profession < 6)
				return true;
			if (Core.SE && profession < 8)
				return true;
			return false;
		}

		private static void AddBackpack(Mobile m)
		{
			var pack = m.Backpack;

			if (pack == null)
			{
				pack = new Backpack();
				pack.Movable = false;

				m.AddItem(pack);
			}

			//PackItem(new RedBook("a book", m.Name, 20, true)); // Junk
			//PackItem(new Candle()); // Junk
			//PackItem(new Gold(1000)); // No starting gold
			
			var starterWeapon = new Dagger();
			starterWeapon.LootType = LootType.Blessed;
			PackItem(starterWeapon); // Everyone gets a blessed dagger
		}

		private static void AddShirt(Mobile m, int shirtHue)
		{
			var hue = Utility.ClipDyedHue(shirtHue & 0x3FFF);

			var allShirt = new Shirt(hue);
			allShirt.LootType = LootType.Blessed;
			EquipItem(allShirt); // Everyone gets a blessed shirt
		}

		private static void AddPants(Mobile m, int pantsHue)
		{
			var hue = Utility.ClipDyedHue(pantsHue & 0x3FFF);

			if (m.Female) {
				var femaleSkirt = new Skirt(hue);
				femaleSkirt.LootType = LootType.Blessed;
				EquipItem(femaleSkirt); // All ladies gets a blessed skirt
			} else {
				var malePants = new ShortPants(hue);
				malePants.LootType = LootType.Blessed;
				EquipItem(malePants); // All dudes gets blessed short pants
			}
		}

		private static void AddShoes(Mobile m)
		{
			var allShoes = new Shoes(Utility.RandomYellowHue());
			allShoes.LootType = LootType.Blessed;
			EquipItem(allShoes); // Everyone gets a pair of blessed shoes
		}

		private static Mobile CreateMobile(Account a)
		{
			if (a.Count >= a.Limit)
				return null;

			for (var i = 0; i < a.Length; ++i)
			{
				if (a[i] == null)
					return (a[i] = new PlayerMobile());
			}

			return null;
		}

		private static void EventSink_CharacterCreated(CharacterCreatedEventArgs args)
		{
			if (!VerifyProfession(args.Profession))
				args.Profession = 0;

			var state = args.State;

			if (state == null)
				return;

			var newChar = CreateMobile(args.Account as Account);

			if (newChar == null)
			{
				Utility.PushColor(ConsoleColor.Red);
				Console.WriteLine("Login: {0}: Character creation failed, account full", state);
				Utility.PopColor();
				return;
			}

			args.Mobile = newChar;
			m_Mobile = newChar;

			newChar.Player = true;
			newChar.AccessLevel = args.Account.AccessLevel;
			newChar.Female = args.Female;
			//newChar.Body = newChar.Female ? 0x191 : 0x190;

			if (Core.Expansion >= args.Race.RequiredExpansion) {
				newChar.Race = args.Race; //Sets body
			} else {
				newChar.Race = Race.DefaultRace;
			}

			newChar.Hue = args.Hue | 0x8000;
			
			if (newChar.Race != Race.DefaultRace) { // Only humans allowed
				newChar.Race = Race.DefaultRace;
				newChar.Hue = 0x00;
				Utility.PushColor(ConsoleColor.Red);
				Console.WriteLine("Login: {0}: Character creation partially failed, non-human race", state);
				Utility.PopColor();
			}

			newChar.Hunger = 20;

			var young = false;

			if (newChar is PlayerMobile)
			{
				var pm = (PlayerMobile)newChar;
				
				pm.AutoRenewInsurance = true;

				var skillcap = Config.Get("PlayerCaps.SkillCap", 1000.0d) / 10;
				
				if (skillcap != 100.0)
				{
					for (var i = 0; i < Enum.GetNames(typeof(SkillName)).Length; ++i)
						pm.Skills[i].Cap = skillcap;
				}
				
				pm.Profession = args.Profession;

				if (pm.IsPlayer() && pm.Account.Young && !Siege.SiegeShard)
					young = pm.Young = true;
			}

			SetName(newChar, args.Name);

			AddBackpack(newChar);

            SetStats(newChar, state, args.Profession, args.Str, args.Dex, args.Int);
			SetSkills(newChar, args.Skills, args.Profession);

			var race = newChar.Race;

			if (race.ValidateHair(newChar, args.HairID))
			{
				newChar.HairItemID = args.HairID;
				newChar.HairHue = args.HairHue;
			}

			if (race.ValidateFacialHair(newChar, args.BeardID))
			{
				newChar.FacialHairItemID = args.BeardID;
				newChar.FacialHairHue = args.BeardHue;
			}

			var faceID = args.FaceID;

			if (faceID > 0 && race.ValidateFace(newChar.Female, faceID))
			{
				newChar.FaceItemID = faceID;
				newChar.FaceHue = args.FaceHue;
			}
			else
			{
				newChar.FaceItemID = race.RandomFace(newChar.Female);
				newChar.FaceHue = newChar.Hue;
			}

			if (args.Profession <= 3)
			{
				AddShirt(newChar, args.ShirtHue);
				AddPants(newChar, args.PantsHue);
				AddShoes(newChar);
			}

			if (TestCenter.Enabled)
				TestCenter.FillBankbox(newChar);

			if (young)
			{
				var ticket = new NewPlayerTicket
				{
					Owner = newChar
				};
				
				newChar.BankBox.DropItem(ticket);
			}

			var city = args.City;
			var map = Siege.SiegeShard && city.Map == Map.Trammel ? Map.Felucca : city.Map;

			newChar.MoveToWorld(city.Location, map);

			Utility.PushColor(ConsoleColor.Green);
			Console.WriteLine("Login: {0}: New character being created (account={1})", state, args.Account.Username);
			Utility.PopColor();
			Utility.PushColor(ConsoleColor.DarkGreen);
			Console.WriteLine(" - Character: {0} (serial={1})", newChar.Name, newChar.Serial);
			Console.WriteLine(" - Started: {0} {1} in {2}", city.City, city.Location, city.Map);
			Utility.PopColor();

			new WelcomeTimer(newChar).Start();
		}

		private static void FixStats(ref int str, ref int dex, ref int intel, int max)
		{
			var vMax = max - 30;

			var vStr = str - 10;
			var vDex = dex - 10;
			var vInt = intel - 10;

			if (vStr < 0)
				vStr = 0;

			if (vDex < 0)
				vDex = 0;

			if (vInt < 0)
				vInt = 0;

			var total = vStr + vDex + vInt;

			if (total == 0 || total == vMax)
				return;

			var scalar = vMax / (double)total;

			vStr = (int)(vStr * scalar);
			vDex = (int)(vDex * scalar);
			vInt = (int)(vInt * scalar);

			FixStat(ref vStr, (vStr + vDex + vInt) - vMax, vMax);
			FixStat(ref vDex, (vStr + vDex + vInt) - vMax, vMax);
			FixStat(ref vInt, (vStr + vDex + vInt) - vMax, vMax);

			str = vStr + 10;
			dex = vDex + 10;
			intel = vInt + 10;
		}

		private static void FixStat(ref int stat, int diff, int max)
		{
			stat += diff;

			if (stat < 0)
				stat = 0;
			else if (stat > max)
				stat = max;
		}

		private static void SetStats(Mobile m, NetState state, int str, int dex, int intel)
		{
			var max = state.NewCharacterCreation ? 90 : 80;

			FixStats(ref str, ref dex, ref intel, max);

			if (str < 10 || str > 60 || dex < 10 || dex > 60 || intel < 10 || intel > 60 || (str + dex + intel) != max)
			{
				str = 10;
				dex = 10;
				intel = 10;
			}

			m.InitStats(str, dex, intel);
		}

		private static void SetName(Mobile m, string name)
		{
			name = name.Trim();

			if (!NameVerification.Validate(name, 2, 16, true, false, true, 1, NameVerification.SpaceDashPeriodQuote))
				name = "Generic Player";

			m.Name = name;
		}

		private static bool ValidSkills(SkillNameValue[] skills)
		{
			var total = 0;

			for (var i = 0; i < skills.Length; ++i)
			{
				if (skills[i].Value < 0 || skills[i].Value > 50)
					return false;

				total += skills[i].Value;

				for (var j = i + 1; j < skills.Length; ++j)
				{
					if (skills[j].Value > 0 && skills[j].Name == skills[i].Name)
						return false;
				}
			}

			return (total == 100 || total == 120);
		}

        private static void SetStats(Mobile m, NetState state, int prof, int str, int dex, int intel)
        {
            switch (prof)
            {
                case 1: // Warrior
                    {
                        str = 45;
                        dex = 35;
                        intel = 10;
                        break;
                    }
                case 2: // Magician
                    {
                        str = 25;
                        dex = 20;
                        intel = 45;
                        break;
                    }
                case 3: // Blacksmith
                    {
                        str = 60;
                        dex = 15;
                        intel = 15;
                        break;
                    }
                case 4: // Necromancer
                    {
                        str = 25;
                        dex = 20;
                        intel = 45;
                        break;
                    }
                case 5: // Paladin
                    {
                        str = 45;
                        dex = 20;
                        intel = 25;
                        break;
                    }
                case 6: //Samurai
                    {
                        str = 40;
                        dex = 30;
                        intel = 20;
                        break;
                    }
                case 7: //Ninja
                    {
                        str = 40;
                        dex = 30;
                        intel = 20;
                        break;
                    }
                default:
                    {
                        SetStats(m, state, str, dex, intel);

                        return;
                    }
            }

            m.InitStats(str, dex, intel);
        }

		private static void SetSkills(Mobile m, SkillNameValue[] skills, int prof)
		{
			switch (prof)
			{
				case 1: // Warrior
				{
					skills = new[]
					{
						new SkillNameValue(SkillName.Swords, 30), new SkillNameValue(SkillName.Parry, 30),
						new SkillNameValue(SkillName.Tactics, 30), new SkillNameValue(SkillName.Anatomy, 30)
					};

					break;
				}
				case 2: // Magician
				{
					skills = new[]
					{
						new SkillNameValue(SkillName.Magery, 30), new SkillNameValue(SkillName.EvalInt, 30),
						new SkillNameValue(SkillName.Meditation, 30), new SkillNameValue(SkillName.Wrestling, 30)
					};

					break;
				}
				case 3: // Blacksmith
				{
					skills = new[]
					{
						new SkillNameValue(SkillName.Mining, 30), new SkillNameValue(SkillName.ArmsLore, 30),
						new SkillNameValue(SkillName.Blacksmith, 30), new SkillNameValue(SkillName.Tinkering, 30)
					};

					break;
				}
				case 4: // Necromancer
				{
					skills = new[]
					{
						new SkillNameValue(SkillName.Necromancy, 30), new SkillNameValue(SkillName.SpiritSpeak, 30),
						new SkillNameValue(SkillName.Meditation, 30), new SkillNameValue(SkillName.Wrestling, 30)
					};

					break;
				}
				case 5: // Paladin
				{
					skills = new[]
					{
						new SkillNameValue(SkillName.Macing, 30), new SkillNameValue(SkillName.Chivalry, 30),
						new SkillNameValue(SkillName.Focus, 30), new SkillNameValue(SkillName.Tactics, 30)
					};

					break;
				}
				case 6: // Samurai
				{
					skills = new[]
					{
						new SkillNameValue(SkillName.Swords, 30), new SkillNameValue(SkillName.Bushido, 30),
						new SkillNameValue(SkillName.Anatomy, 30), new SkillNameValue(SkillName.Healing, 30)
					};
					break;
				}
				case 7: // Ninja
				{
					skills = new[]
					{
						new SkillNameValue(SkillName.Ninjitsu, 30), new SkillNameValue(SkillName.Hiding, 30),
						new SkillNameValue(SkillName.Fencing, 30), new SkillNameValue(SkillName.Stealth, 30)
					};
					break;
				}
				default:
				{
					if (!ValidSkills(skills))
						return;

					break;
				}
			}

			var addSkillItems = true;
			var elf = (m.Race == Race.Elf);
			var human = (m.Race == Race.Human);
			var gargoyle = (m.Race == Race.Gargoyle);

			for (var i = 0; i < skills.Length; ++i)
			{
				var snv = skills[i];

				if (snv.Value > 0 && (snv.Name != SkillName.Stealth || prof == 7) && snv.Name != SkillName.RemoveTrap &&
					snv.Name != SkillName.Spellweaving)
				{
					var skill = m.Skills[snv.Name];

					if (skill != null)
					{
						skill.BaseFixedPoint = snv.Value * 10;

						if (addSkillItems)
							AddSkillItems(snv.Name, m);
					}
				}
			}
		}

		private static void EquipItem(Item item)
		{
			EquipItem(item, false);
		}

		private static void EquipItem(Item item, bool mustEquip)
		{
			if (!Core.AOS)
				item.LootType = LootType.Newbied;

			if (m_Mobile != null && m_Mobile.EquipItem(item))
				return;

			var pack = m_Mobile.Backpack;

			if (!mustEquip && pack != null)
				pack.DropItem(item);
			else
				item.Delete();
		}

		private static void PackItem(Item item)
		{
			if (!Core.AOS)
				item.LootType = LootType.Newbied;

			var pack = m_Mobile.Backpack;

			if (pack != null)
				pack.DropItem(item);
			else
				item.Delete();
		}

		private static void PackInstrument()
		{
			switch (Utility.Random(6))
			{
				case 0:
					PackItem(new Drums());
					break;
				case 1:
					PackItem(new Harp());
					break;
				case 2:
					PackItem(new LapHarp());
					break;
				case 3:
					PackItem(new Lute());
					break;
				case 4:
					PackItem(new Tambourine());
					break;
				case 5:
					PackItem(new TambourineTassel());
					break;
			}
		}

		private static void PackScroll(int circle)
		{
			switch (Utility.Random(8)) //* (circle + 1))
			{
				case 0:
					PackItem(new ClumsyScroll());
					break;
				case 1:
					PackItem(new CreateFoodScroll());
					break;
				case 2:
					PackItem(new FeeblemindScroll());
					break;
				case 3:
					PackItem(new HealScroll());
					break;
				case 4:
					PackItem(new MagicArrowScroll());
					break;
				case 5:
					PackItem(new NightSightScroll());
					break;
				case 6:
					PackItem(new ReactiveArmorScroll());
					break;
				case 7:
					PackItem(new WeakenScroll());
					break;
			}
		}

		private static void AddSkillItems(SkillName skill, Mobile m)
		{
			var elf = (m.Race == Race.Elf);
			var human = (m.Race == Race.Human);
			var gargoyle = (m.Race == Race.Gargoyle);

			switch (skill)
			{
				case SkillName.Alchemy:
				{
					PackItem(new Bottle(10));
					PackItem(new MortarPestle());
					PackItem(new BagOfAllReagents());

					EquipItem(new Robe(Utility.RandomPinkHue()));

					break;
				}
				case SkillName.Anatomy:
				{
					PackItem(new Bandage(10));

					break;
				}
				case SkillName.AnimalLore:
				{
					EquipItem(new ShepherdsCrook());

					EquipItem(new Robe(Utility.RandomGreenHue()));

					break;
				}
				case SkillName.Archery:
				{
					PackItem(new Arrow(25));

					EquipItem(new Bow());

					break;
				}
				case SkillName.ArmsLore:
				{
					EquipItem(new Katana());
					EquipItem(new Kryss());
					EquipItem(new Club());
					
					break;
				}
				case SkillName.Begging:
				{
					EquipItem(new GnarledStaff());

					EquipItem(new Robe(Utility.RandomYellowHue()));

					break;
				}
				case SkillName.Blacksmith:
				{
					PackItem(new Tongs());
					PackItem(new Pickaxe());

					EquipItem(new HalfApron(Utility.RandomYellowHue()));

					break;
				}
				case SkillName.Bushido:
				{
					EquipItem(new Hakama());
					EquipItem(new Kasa());

					EquipItem(new BookOfBushido());

					break;
				}
				case SkillName.Fletching:
				{
					PackItem(new Feather(10));
					PackItem(new Shaft(10));

					break;
				}
				case SkillName.Camping:
				{
					PackItem(new Bedroll());
					PackItem(new Kindling(10));

					break;
				}
				case SkillName.Carpentry:
				{
					PackItem(new Board(10));
					PackItem(new Saw());

					EquipItem(new HalfApron(Utility.RandomYellowHue()));

					break;
				}
				case SkillName.Cartography:
				{
					PackItem(new BlankMap());
					PackItem(new BlankMap());
					PackItem(new BlankMap());
					PackItem(new BlankMap());
					PackItem(new Sextant());

					break;
				}
				case SkillName.Cooking:
				{
					PackItem(new Kindling(5));
					PackItem(new RawLambLeg());
					PackItem(new RawChickenLeg());
					PackItem(new RawFishSteak());
					PackItem(new SackFlour());
					PackItem(new Pitcher(BeverageType.Water));

					break;
				}
				case SkillName.Chivalry:
				{
					if (Core.ML)
						PackItem(new BookOfChivalry((ulong)0x3FF));

					break;
				}
				case SkillName.DetectHidden:
				{
					EquipItem(new Cloak(0x455));

					break;
				}
				case SkillName.Discordance:
				{
					PackInstrument();

					break;
				}
				case SkillName.Fencing:
				{
					EquipItem(new Kryss());

					break;
				}
				case SkillName.Fishing:
				{
					EquipItem(new FishingPole());

					EquipItem(new FloppyHat(Utility.RandomYellowHue()));

					break;
				}
				case SkillName.Healing:
				{
					PackItem(new Bandage(20));
					PackItem(new Scissors());

					break;
				}
				case SkillName.Herding:
				{
					EquipItem(new ShepherdsCrook());

					break;
				}
				case SkillName.Hiding:
				{
					EquipItem(new Cloak(0x455));

					break;
				}
				case SkillName.Inscribe:
				{
					PackItem(new BlankScroll(5));
					PackItem(new BlueBook());
					
					PackScroll(0);

					break;
				}
				case SkillName.ItemID:
				{
					EquipItem(new GnarledStaff());

					break;
				}
				case SkillName.Lockpicking:
				{
					PackItem(new Lockpick(10));

					break;
				}
				case SkillName.Lumberjacking:
				{
					EquipItem(new Hatchet());

					break;
				}
				case SkillName.Macing:
				{
					EquipItem(new Club());

					break;
				}
				case SkillName.Magery:
				{
					PackItem(new Spellbook((ulong)0x382A8C38));

					EquipItem(new WizardsHat(Utility.RandomBlueHue()));
					EquipItem(new Robe(Utility.RandomBlueHue()));

					break;
				}
				case SkillName.Mining:
				{
					PackItem(new Pickaxe());
					PackItem(new Pickaxe());

					break;
				}
				case SkillName.Musicianship:
				{
					PackInstrument();

					break;
				}
				case SkillName.Necromancy:
				{
					PackItem(new NecromancerSpellbook((ulong)0x8981));

					EquipItem(new WizardsHat(Utility.RandomRedHue()));
					EquipItem(new Robe(Utility.RandomRedHue()));

					break;
				}
				case SkillName.Ninjitsu:
				{
					EquipItem(new Hakama(0x2C3)); //Only ninjas get the hued one.
					EquipItem(new Kasa());

					EquipItem(new BookOfNinjitsu());

					break;
				}
				case SkillName.Parry:
				{
					EquipItem(new WoodenShield());

					break;
				}
				case SkillName.Peacemaking:
				{
					PackInstrument();

					break;
				}
				case SkillName.Poisoning:
				{
					PackItem(new LesserPoisonPotion(5));

					break;
				}
				case SkillName.Provocation:
				{
					PackInstrument();

					break;
				}
				case SkillName.Snooping:
				{
					PackItem(new Lockpick(5));

					break;
				}
				case SkillName.EvalInt:
				{
					EquipItem(new Cloak(Utility.RandomBlueHue()));

					break;
				}
				case SkillName.SpiritSpeak:
				{
					EquipItem(new Cloak(Utility.RandomRedHue()));

					break;
				}
				case SkillName.Stealing:
				{
					PackItem(new Lockpick(5));

					break;
				}
				case SkillName.Swords:
				{
					EquipItem(new Katana());

					break;
				}
				case SkillName.Tactics:
				{
					EquipItem(new LeatherChest());

					break;
				}
				case SkillName.Tailoring:
				{
					PackItem(new BoltOfCloth());
					PackItem(new SewingKit());

					break;
				}
				case SkillName.Tinkering:
				{
					PackItem(new TinkerTools());
					PackItem(new Axle());
					PackItem(new AxleGears());
					PackItem(new Springs());
					PackItem(new ClockFrame());

					break;
				}
				case SkillName.Tracking:
				{
					EquipItem(new SkinningKnife());

					break;
				}
				case SkillName.Veterinary:
				{
					PackItem(new Bandage(5));
					PackItem(new Scissors());

					break;
				}
				case SkillName.Wrestling:
				{
					EquipItem(new LeatherGloves());

					break;
				}
				case SkillName.Throwing:
				{
					if (gargoyle)
						EquipItem(new Boomerang());

					break;
				}
				case SkillName.Mysticism:
				{
					PackItem(new MysticBook((ulong)0xAB));

					EquipItem(new WizardsHat());
					EquipItem(new Robe());

					break;
				}
			}
		}

		private class BadStartMessage : Timer
		{
			readonly Mobile m_Mobile;
			readonly int m_Message;

			public BadStartMessage(Mobile m, int message)
				: base(TimeSpan.FromSeconds(3.5))
			{
				m_Mobile = m;
				m_Message = message;
				Start();
			}

			protected override void OnTick()
			{
				m_Mobile.SendLocalizedMessage(m_Message);
			}
		}
	}
}