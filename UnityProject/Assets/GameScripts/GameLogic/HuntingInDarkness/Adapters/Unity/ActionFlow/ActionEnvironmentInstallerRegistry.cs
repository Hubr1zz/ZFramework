using System;
using System.Collections.Generic;
using UnityEngine;

namespace HuntingInDarkness.ActionFlow
{
    /// <summary>把一个战役级效果适配到它关心的 ActionEnvironment；领域状态仍由调用方持有。</summary>
    public interface IActionEnvironmentInstaller
    {
        bool Supports(ActionEnvironmentKind kind);
        void Install(IActionEnvironment environment, ActionEnvironmentInstallation installation);
    }

    public interface IActionEnvironmentInstallerRegistry
    {
        int InstallerCount { get; }
        int AttachedEnvironmentCount { get; }
        IDisposable Register(IActionEnvironmentInstaller installer);
        IDisposable Attach(IActionEnvironment environment);
    }

    /// <summary>记录一次效果安装产生的 Reactor、Gate 或其他租约，并按逆序统一释放。</summary>
    public sealed class ActionEnvironmentInstallation : IDisposable
    {
        private readonly List<IDisposable> leases = new();
        private bool disposed;

        public int Count => leases.Count;

        public IDisposable Add(IDisposable lease)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ActionEnvironmentInstallation));
            if (lease == null) throw new ArgumentNullException(nameof(lease));
            leases.Add(lease);
            return lease;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            for (int index = leases.Count - 1; index >= 0; index--)
            {
                try
                {
                    leases[index].Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
            leases.Clear();
        }
    }

    /// <summary>
    /// 战役生命周期内的环境装配目录。Installer 注册一次后会投影到所有匹配的现有环境，
    /// 也会自动进入之后创建的阶段环境；环境退出时只释放该环境的安装租约。
    /// </summary>
    public sealed class ActionEnvironmentInstallerRegistry : IActionEnvironmentInstallerRegistry, IDisposable
    {
        private readonly List<InstallerRegistration> installers = new();
        private readonly List<EnvironmentRegistration> environments = new();
        private bool disposed;

        public int InstallerCount => installers.Count;
        public int AttachedEnvironmentCount => environments.Count;

        public IDisposable Register(IActionEnvironmentInstaller installer)
        {
            ThrowIfDisposed();
            if (installer == null) throw new ArgumentNullException(nameof(installer));

            var registration = new InstallerRegistration(this, installer);
            var installedEnvironments = new List<EnvironmentRegistration>();
            try
            {
                EnvironmentRegistration[] snapshot = environments.ToArray();
                foreach (EnvironmentRegistration environment in snapshot)
                {
                    if (!installer.Supports(environment.Environment.Kind)) continue;
                    environment.Install(registration);
                    installedEnvironments.Add(environment);
                }
                ThrowIfDisposed();
                installers.Add(registration);
                return registration;
            }
            catch
            {
                for (int index = installedEnvironments.Count - 1; index >= 0; index--)
                    installedEnvironments[index].Remove(registration);
                throw;
            }
        }

        public IDisposable Attach(IActionEnvironment environment)
        {
            ThrowIfDisposed();
            if (environment == null) throw new ArgumentNullException(nameof(environment));
            if (environment.IsDisposed) throw new ObjectDisposedException(environment.Name);
            foreach (EnvironmentRegistration existing in environments)
                if (ReferenceEquals(existing.Environment, environment))
                    throw new InvalidOperationException($"Action environment '{environment.Name}' is already attached.");

            var registration = new EnvironmentRegistration(this, environment);
            try
            {
                InstallerRegistration[] snapshot = installers.ToArray();
                foreach (InstallerRegistration installer in snapshot)
                    if (installer.Installer.Supports(environment.Kind))
                        registration.Install(installer);
                ThrowIfDisposed();
                if (environment.IsDisposed) throw new ObjectDisposedException(environment.Name);
                environments.Add(registration);
                return registration;
            }
            catch
            {
                registration.ReleaseAll();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            for (int index = environments.Count - 1; index >= 0; index--)
                environments[index].ReleaseAll();
            environments.Clear();
            foreach (InstallerRegistration installer in installers)
                installer.MarkDisposed();
            installers.Clear();
        }

        private void Remove(InstallerRegistration installer)
        {
            if (!installers.Remove(installer)) return;
            EnvironmentRegistration[] snapshot = environments.ToArray();
            for (int index = snapshot.Length - 1; index >= 0; index--)
                snapshot[index].Remove(installer);
            installer.MarkDisposed();
        }

        private void Detach(EnvironmentRegistration environment)
        {
            if (!environments.Remove(environment)) return;
            environment.ReleaseAll();
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(ActionEnvironmentInstallerRegistry));
        }

        private sealed class InstallerRegistration : IDisposable
        {
            private readonly ActionEnvironmentInstallerRegistry owner;
            private bool isDisposed;

            public InstallerRegistration(ActionEnvironmentInstallerRegistry owner, IActionEnvironmentInstaller installer)
            {
                this.owner = owner;
                Installer = installer;
            }

            public IActionEnvironmentInstaller Installer { get; }

            public void Dispose()
            {
                if (isDisposed) return;
                owner.Remove(this);
            }

            public void MarkDisposed() => isDisposed = true;
        }

        private sealed class EnvironmentRegistration : IDisposable
        {
            private readonly ActionEnvironmentInstallerRegistry owner;
            private readonly List<InstalledEffect> effects = new();
            private bool isDisposed;

            public EnvironmentRegistration(ActionEnvironmentInstallerRegistry owner, IActionEnvironment environment)
            {
                this.owner = owner;
                Environment = environment;
            }

            public IActionEnvironment Environment { get; }

            public void Install(InstallerRegistration installer)
            {
                var installation = new ActionEnvironmentInstallation();
                try
                {
                    installer.Installer.Install(Environment, installation);
                    effects.Add(new InstalledEffect(installer, installation));
                }
                catch
                {
                    installation.Dispose();
                    throw;
                }
            }

            public void Remove(InstallerRegistration installer)
            {
                for (int index = effects.Count - 1; index >= 0; index--)
                {
                    if (!ReferenceEquals(effects[index].Installer, installer)) continue;
                    effects[index].Installation.Dispose();
                    effects.RemoveAt(index);
                }
            }

            public void Dispose()
            {
                if (isDisposed) return;
                owner.Detach(this);
            }

            public void ReleaseAll()
            {
                if (isDisposed) return;
                isDisposed = true;
                for (int index = effects.Count - 1; index >= 0; index--)
                    effects[index].Installation.Dispose();
                effects.Clear();
            }
        }

        private readonly struct InstalledEffect
        {
            public InstalledEffect(InstallerRegistration installer, ActionEnvironmentInstallation installation)
            {
                Installer = installer;
                Installation = installation;
            }

            public InstallerRegistration Installer { get; }
            public ActionEnvironmentInstallation Installation { get; }
        }
    }
}
