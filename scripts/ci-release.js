const fs = require('fs');
const path = require('path');
const glob = require('glob');
const { execSync } = require('child_process');

const OUT_DIR = path.join(__dirname, '../out');

const packageJson = require('../package.json');

function exec(command, throwOnError = true) {
  const r = execSync(command, { stdio: 'inherit' });
  if (throwOnError && r.status !== 0) {
    throw new Error(`Command failed with status ${r.status}: ${command}`);
  }
}

function copyFile(source, target) {
  fs.writeFileSync(target, fs.readFileSync(source));
}

function copyFolder(source, target) {
  fs.mkdirSync(target);
  fs.readdirSync(source).forEach((file) => {
    const curSource = path.join(source, file);
    if (fs.lstatSync(curSource).isDirectory()) {
      copyFolder(curSource, path.join(target, file));
    } else {
      copyFile(curSource, path.join(target, file));
    }
  });
}

function info(dir) {
  const projfilepath = glob.sync(path.join(dir, '*.csproj'))[0];
  const projfile = fs.readFileSync(projfilepath).toString()
  const manifest = fs.readFileSync(path.join(dir, 'extension.yaml')).toString();
  const assemblyName = projfile.match(/<AssemblyName>(.+)<\/AssemblyName>/)[1];
  const name = manifest.match(/Name: (.+)/)[1];
  const version = manifest.match(/Version: (.+)/)[1];
  return { projfilepath, projfile, manifest, name, version, assemblyName };
}

function build(dir) {
  const cwd = process.cwd();
  process.chdir(dir);
  try {
    exec('yarn restore');
    const { name, version, projfilepath, projfile } = info(dir);

    console.log(`Building ${name} ${version}...`);
    // update version
    const versStr = `<AssemblyVersion>${version}</AssemblyVersion>`;
    if (projfile.includes(versStr)) {
      return false;
    }
    projfile = projfile.replace(/<AssemblyVersion>(.+)<\/AssemblyVersion>/, versStr);
    fs.writeFileSync(projfilepath, projfile);

    // build
    exec('yarn release');
    return true;
  } finally {
    process.chdir(cwd);
  }
}

function changelog(dir) {
  const cwd = process.cwd();
  process.chdir(dir);
  try {
    exec('yarn changelog');

    const { name, version } = info(dir);

    // if changelog already exists at out dir, append to it
    const changelogPath = path.join(OUT_DIR, `CHANGELOG.md`);
    if (fs.existsSync(changelogPath)) {
      const changelog = `##${name} ${version}\n` + fs.readFileSync(changelogPath).toString();
      const newChangelog = fs.readFileSync(path.join(dir, 'CHANGELOG.md')).toString();
      fs.writeFileSync(changelogPath, `${newChangelog}\n${changelog}`);
    } else {
      copyFile(path.join(dir, 'CHANGELOG.md'), changelogPath);
    }


  } finally {
    process.chdir(cwd);
  }
}
