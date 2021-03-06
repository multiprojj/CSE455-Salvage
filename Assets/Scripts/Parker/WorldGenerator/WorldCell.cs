﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Xml;

/// <summary>
/// World cell class used for each spawning area of asteroids
/// </summary>
public class WorldCell : MonoBehaviour 
{

	#region Saving Path ; where to save the game according to platform
	#if UNITY_EDITOR
	public string mainDirectory = Application.dataPath + "/SaveData/";
	public string directory = Application.dataPath + "/SaveData/";
	#else
	public string mainDirectory = Application.persistentDataPath + "/SaveData/";
	public string directory = Application.persistentDataPath + "/SaveData/";
	#endif
	#endregion

	#region data ; for this class
	public enum CellStatus{ active, standby, init };
	private CellStatus status = CellStatus.standby;

	public GameObject parent;
	private string worldName; //name of the generated world
	private string cellName; //id of this cell
	private string fileName; //the exact file we are either saving or loading from
	private bool startRan = false; //has the start function ran this game for this cell
	public bool deactivateNow = false;
	public bool activateNow = false;
	public bool hasPlanet = false;

	public List<WorldCell> neighbors = new List<WorldCell>();

	public List<GameObject> children = new List<GameObject>();
	private List<Vector2> positions = new List<Vector2>();
	private List<float> perlin = new List<float>();

	#pragma warning disable 0414
	private float distanceFromCenter = 0.0f; //this is going to be used to add varience in the spawned asteriods
	#pragma warning restore
	#endregion

	public void CheckPlayer()
	{
		if(GameManager.Instance.playerObject)
		{
			float distance = Vector3.Distance(gameObject.transform.position, GameManager.Instance.playerObject.transform.position);
			float cellSize = gameObject.transform.localScale.x;
			if(distance <= (cellSize * 2) && status != CellStatus.active)
			{
				Activate();
				status = CellStatus.active;
			}
			else if(distance > (cellSize * 2) && status != CellStatus.standby)
			{
				status = CellStatus.standby;
				Deactivate();
			}
		}
	}
	//first function that runs upon startup of object
	public void Start()
	{
		if(!startRan)
		{	
			cellName = gameObject.name;
			worldName = gameObject.transform.parent.name;
			distanceFromCenter = Vector2.Distance(Vector2.zero, gameObject.transform.position );
			if(!Directory.Exists(mainDirectory))
			{
				Directory.CreateDirectory(mainDirectory);
			}
			directory += "/" + worldName + "/";
			fileName += directory + cellName + ".xml";
			startRan = true;

			if(!parent)
			{
				parent = new GameObject("Asteroids");
			}
			parent.transform.position = transform.position;
			parent.transform.parent = transform;
			parent.transform.localScale = Vector3.one;
		}
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		if(other.tag == "Player")
		{
			foreach(WorldCell obj in neighbors)
			{
				obj.CheckPlayer();
			}
		}
	}

	//activate the cell so that it spawns all necessary objects.
	public void Activate()
	{
		ObjectPool.Pool.ActiveCells.Add(this);
		if(!startRan)
		{
			Start();
		}
		if (Directory.Exists(directory) && File.Exists(fileName)) 
		{
			Load ();
		}
		else
		{
			GenerateXMLData();
			Load();
		}
	}

	//turns the object off
	public void Deactivate()
	{
		ObjectPool.Pool.ActiveCells.Remove (this);

		AutoSave ();
		foreach(GameObject obj in children)
		{
			ObjectPool.Pool.MarkUnused (obj);
		}

		children.Clear();
	}

	public static IEnumerator GenerateALLXMLData (float Range)
	{
#if UNITY_EDITOR
		string tempDir = Application.dataPath + "/SaveData/";
#else
		string tempDir = Application.persistentDataPath + "/SaveData/";
#endif
		if(!Directory.Exists(tempDir))
		{
			Directory.CreateDirectory(tempDir);
		}
		tempDir += WorldGenerator.worldspec.spaceName + "/";
		if(!Directory.Exists(tempDir))
		{
			Directory.CreateDirectory(tempDir);
		}

		WorldGenerator.WorldSpecs details = WorldGenerator.worldspec;
		Random.seed = details.seed;
		float halfCellLength = Mathf.Ceil(details.cellLength/2.0f);
		Vector2 startingPos = new Vector2( -halfCellLength, -halfCellLength);
		Vector2 endPos = new Vector2( halfCellLength, halfCellLength);

		foreach( WorldCell cell in ObjectPool.Pool.cells)
		{
			cell.Start();
			if(Vector2.Distance(cell.transform.position,Vector2.zero) < Range)
			{
				if(!cell.hasPlanet)
				{
					for(int i = (int)startingPos.x; i < endPos.x ; i++)
					{
						for(int j = (int)startingPos.y; j < endPos.y ; j++)
						{				
							float xCoord = ((i + cell.transform.position.x) + details.mapLength /2) / (float)details.mapLength * 25.6f;
							float yCoord = ((j + cell.transform.position.y) + details.mapLength /2) / (float)details.mapLength * 25.6f;						
							float scale = Mathf.PerlinNoise(xCoord,yCoord);
							
							if(scale > 0.95f || scale < 0.1f || (scale > 0.45f && scale < 0.5f))
							{
								cell.positions.Add(new Vector2(i + cell.transform.position.x, j + cell.transform.position.y));
								cell.perlin.Add(scale);
							}
						}
					}
					
					XmlTextWriter writer = new XmlTextWriter (cell.fileName, System.Text.Encoding.UTF8);
					
					writer.WriteStartDocument();
					writer.WriteWhitespace("\n");
					writer.WriteStartElement("Root");
					writer.WriteWhitespace("\n");
					
					for(int i = 0, j = 0; i < cell.positions.Count && j < cell.perlin.Count; i++, j++)
					{
						writer.WriteWhitespace("\t");
						writer.WriteStartElement("AsteroidPosition");
						writer.WriteAttributeString("x ",cell.positions[i].x.ToString());
						writer.WriteAttributeString("y ",cell.positions[i].y.ToString());
						writer.WriteEndElement();
						writer.WriteWhitespace("\n\t\t");
						writer.WriteElementString("PerlinValue", cell.perlin[j].ToString());
						writer.WriteWhitespace("\n");
					}
					
					writer.WriteEndDocument ();
					writer.Close ();
				}
				else
				{
					XmlTextWriter writer = new XmlTextWriter (cell.fileName, System.Text.Encoding.UTF8);
					
					writer.WriteStartDocument();
					writer.WriteWhitespace("\n");
					writer.WriteStartElement("Root");
					writer.WriteEndElement();
					writer.WriteWhitespace("\n");
					writer.WriteEndDocument();
					writer.Close ();
				}
			}
		}
	
		yield return new WaitForSeconds(0);
	}


	//if this is the first time being activated the cell will call this to spawn the asteroids
	public void GenerateXMLData ()
	{
		if(!Directory.Exists(directory))
		{
			Directory.CreateDirectory(directory);
		}

		if(!hasPlanet)
		{
			WorldGenerator.WorldSpecs details = WorldGenerator.worldspec;

			Random.seed = details.seed;
			float maxDistance = details.mapLength / 2.0f;
			float halfCellLength = Mathf.Ceil(details.cellLength/2.0f);
			Vector2 startingPos = new Vector2( -halfCellLength, -halfCellLength);
			Vector2 endPos = new Vector2( halfCellLength, halfCellLength);

			for(int i = (int)startingPos.x; i < endPos.x ; i++)
			{
				for(int j = (int)startingPos.y; j < endPos.y ; j++)
				{				
					float distance = Mathf.Sqrt( Mathf.Pow(i + transform.position.x,2) + Mathf.Pow(j + transform.position.y ,2));
					if(distance < maxDistance && distance > maxDistance / 10.0)
					{
						float xCoord = ((i + transform.position.x) + details.mapLength /2) / (float)details.mapLength * 25.6f;
						float yCoord = ((j + transform.position.y) + details.mapLength /2) / (float)details.mapLength * 25.6f;						
						float scale = Mathf.PerlinNoise(xCoord,yCoord);

						if(scale > 0.95f || scale < 0.1f || (scale > 0.45f && scale < 0.5f))
						{
							positions.Add( new Vector2(i + transform.position.x, j + transform.position.y));
							perlin.Add(scale);
						}
					}
				}
			}

			XmlTextWriter writer = new XmlTextWriter (fileName, System.Text.Encoding.UTF8);
			
			writer.WriteStartDocument();
			writer.WriteWhitespace("\n");
			writer.WriteStartElement("Root");
			writer.WriteWhitespace("\n");
			
			for(int i = 0, j = 0; i < positions.Count && j < perlin.Count; i++, j++)
			{
				writer.WriteWhitespace("\t");
				writer.WriteStartElement("AsteroidPosition");
				writer.WriteAttributeString("x ",positions[i].x.ToString());
				writer.WriteAttributeString("y ",positions[i].y.ToString());
				writer.WriteEndElement();
				writer.WriteWhitespace("\n\t\t");
				writer.WriteElementString("PerlinValue", perlin[j].ToString());
				writer.WriteWhitespace("\n");
			}
			
			writer.WriteEndDocument ();
			writer.Close ();
		}
		else
		{
			XmlTextWriter writer = new XmlTextWriter (fileName, System.Text.Encoding.UTF8);
			
			writer.WriteStartDocument();
			writer.WriteWhitespace("\n");
			writer.WriteStartElement("Root");
			writer.WriteEndElement();
			writer.WriteWhitespace("\n");
			writer.WriteEndDocument();
			writer.Close ();
		}
	}
	
	//saves the current objects in the cell as well as their positions
	public void Save()
	{
		if(!Directory.Exists(directory))
		{
			Directory.CreateDirectory(directory);
		}
		if(File.Exists(fileName) && positions.Count > 0)
		{
			XmlTextWriter writer = new XmlTextWriter (fileName, System.Text.Encoding.UTF8);

			writer.WriteStartDocument();
			writer.WriteWhitespace("\n");
			writer.WriteStartElement("Root");
			writer.WriteWhitespace("\n");

			for(int i = 0, j = 0; i < positions.Count && j < perlin.Count; i++, j++)
			{
				writer.WriteWhitespace("\t");
				writer.WriteStartElement("AsteroidPosition");
				writer.WriteAttributeString("x ",positions[i].x.ToString());
				writer.WriteAttributeString("y ",positions[i].y.ToString());
				writer.WriteEndElement();
				writer.WriteWhitespace("\n\t\t");
				writer.WriteElementString("PerlinValue", perlin[j].ToString());
				writer.WriteWhitespace("\n");
			}

			writer.WriteEndDocument ();
			writer.Close ();
		}
		GameManager.Instance.AddToSavePercentage();
	}

	public void AutoSave()
	{
		if(!Directory.Exists(directory))
		{
			Directory.CreateDirectory(directory);
		}
		if(File.Exists(fileName) && positions.Count > 0)
		{
			XmlTextWriter writer = new XmlTextWriter (fileName, System.Text.Encoding.UTF8);
			
			writer.WriteStartDocument();
			writer.WriteWhitespace("\n");
			writer.WriteStartElement("Root");
			writer.WriteWhitespace("\n");
			
			for(int i = 0, j = 0; i < positions.Count && j < perlin.Count; i++, j++)
			{
				writer.WriteWhitespace("\t");
				writer.WriteStartElement("AsteroidPosition");
				writer.WriteAttributeString("x ",positions[i].x.ToString());
				writer.WriteAttributeString("y ",positions[i].y.ToString());
				writer.WriteEndElement();
				writer.WriteWhitespace("\n\t\t");
				writer.WriteElementString("PerlinValue", perlin[j].ToString());
				writer.WriteWhitespace("\n");
			}
			
			writer.WriteEndDocument ();
			writer.Close ();
		}
	}

	//loads all the objects in the cell
	public void Load()
	{
		if(File.Exists(fileName) && status != CellStatus.active)
		{
			if(positions.Count <= 0 && perlin.Count <= 0)
			{
				positions = new List<Vector2>();
				perlin = new List<float>();

				XmlTextReader reader = new XmlTextReader(fileName);

				while(reader.Read())
				{
					if(reader.IsStartElement() && reader.NodeType == XmlNodeType.Element)
					{
						switch(reader.Name)
						{
							case "AsteroidPosition" :
								positions.Add(new Vector2(float.Parse(reader.GetAttribute(0)), float.Parse(reader.GetAttribute(1))));
								break;

							case "PerlinValue":
								perlin.Add( float.Parse(reader.ReadElementString()));
								break;
						}
					}
				}
				reader.Close ();
			}
			children.Clear ();
			Vector2 indexes = ObjectPool.Pool.Redirect(positions, perlin, this);

			if(indexes.x >= 0 && indexes.x < positions.Count)
			{
				for(int i = (int)indexes.x, j = (int)indexes.y; i < positions.Count && j < perlin.Count; i++,j++)
				{
					GameObject asteroidOBJ = GameObject.Instantiate(Resources.Load("Asteroid/Asteroid")) as GameObject;
					asteroidOBJ.transform.parent = parent.transform;
					Asteroid temp =	asteroidOBJ.AddComponent<Asteroid>();
					temp.assignedPosition = positions[i];
					temp.perlinValue = perlin[j];
					temp.parentCell = this;
					temp.Change();
					children.Add(asteroidOBJ);
					if(ObjectPool.Pool.CanPoolMore())
					{
						ObjectPool.Pool.Register(asteroidOBJ);
					}
				}
			}
		}
	}

	public void RemoveAsteroidPosition(Vector2 position)
	{
		if(positions.Count > 0)
		{
			if(positions.Contains(position))
			{
				positions.Remove(position);
			}
		}
	}
}
